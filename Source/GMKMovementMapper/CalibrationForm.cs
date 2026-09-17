using GMKMovementMapper.Input;
using GMKMovementMapper.Mapping;

namespace GMKMovementMapper;

public sealed class CalibrationForm : Form
{
    private readonly MovementProfile _profile;
    private readonly Label _instruction = new();
    private readonly Label _reading = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _button = new();
    private int _stage;

    public CalibrationForm(MovementProfile profile)
    {
        _profile = profile;
        Text = "Calibrate GMK";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(500, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = profile.DarkMode ? Color.FromArgb(11, 17, 27) : Color.FromArgb(242, 245, 249);
        ForeColor = profile.DarkMode ? Color.White : Color.FromArgb(28, 37, 51);
        Font = new Font("Segoe UI", 10F);

        var title = new Label { Text = "Stick calibration", AutoSize = true, Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold), Location = new Point(28, 24), ForeColor = ForeColor };
        _instruction.SetBounds(30, 78, 440, 58); _instruction.ForeColor = ForeColor;
        _reading.SetBounds(30, 143, 440, 28); _reading.ForeColor = profile.DarkMode ? Color.FromArgb(156, 169, 188) : Color.DimGray;
        _progress.SetBounds(30, 182, 440, 18);
        _button.SetBounds(30, 222, 440, 44); _button.Text = "Start centre calibration"; _button.BackColor = Color.FromArgb(37, 99, 235); _button.ForeColor = Color.White; _button.FlatStyle = FlatStyle.Flat; _button.FlatAppearance.BorderSize = 0;
        _button.Click += async (_, _) => await RunStageAsync();
        Controls.AddRange([title, _instruction, _reading, _progress, _button]);
        SetInstructions();
    }

    private void SetInstructions()
    {
        _instruction.Text = _stage == 0
            ? "Release the stick completely. The program will measure its natural centre for two seconds."
            : "Move the stick slowly around its full outer edge until the bar finishes.";
    }

    private async Task RunStageAsync()
    {
        _button.Enabled = false;
        _progress.Value = 0;
        try
        {
            using var source = new GmkUsbSource();
            source.Connect();
            if (_stage == 0)
            {
                var values = await SampleAsync(source, 2000, false);
                _profile.CenterX = values.CenterX; _profile.CenterY = values.CenterY;
                _stage = 1; _button.Text = "Start full-range calibration"; SetInstructions();
            }
            else
            {
                var values = await SampleAsync(source, 5000, true);
                _profile.MinX = values.MinX; _profile.MaxX = values.MaxX;
                _profile.MinY = values.MinY; _profile.MaxY = values.MaxY;
                _profile.Save();
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Calibration could not start", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { _button.Enabled = true; }
    }

    private async Task<(double CenterX, double CenterY, double MinX, double MaxX, double MinY, double MaxY)> SampleAsync(GmkUsbSource source, int duration, bool range)
    {
        return await Task.Run(() =>
        {
            var started = Environment.TickCount64;
            var xs = new List<double>(); var ys = new List<double>();
            var minX = 1d; var maxX = -1d; var minY = 1d; var maxY = -1d;
            while (Environment.TickCount64 - started < duration)
            {
                if (!source.TryRead(out var x, out var y, out _)) continue;
                xs.Add(x); ys.Add(y); minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                var percent = (int)Math.Clamp((Environment.TickCount64 - started) * 100 / duration, 0, 100);
                BeginInvoke((Action)(() => { _progress.Value = percent; _reading.Text = $"X {x:+0.000;-0.000;0.000}   Y {y:+0.000;-0.000;0.000}"; }));
            }
            if (xs.Count == 0) throw new InvalidOperationException("No input was received from the GMK.");
            return (xs.Average(), ys.Average(), range ? minX : -1, range ? maxX : 1, range ? minY : -1, range ? maxY : 1);
        });
    }
}
