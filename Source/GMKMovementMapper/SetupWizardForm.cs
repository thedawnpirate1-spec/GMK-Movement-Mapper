using GMKMovementMapper.HidHide;
using GMKMovementMapper.Input;

namespace GMKMovementMapper;

public sealed class SetupWizardForm : Form
{
    private readonly HidHideBridge _hidHide = new();
    private readonly Label _status = new();

    public SetupWizardForm(bool dark)
    {
        Text = "GMK Setup Wizard";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 430);
        MinimumSize = new Size(560, 430);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5F);
        var background = dark ? Color.FromArgb(11, 17, 27) : Color.FromArgb(242, 245, 249);
        var text = dark ? Color.White : Color.FromArgb(28, 37, 51);
        BackColor = background; ForeColor = text;

        var title = new Label { Text = "Set up your GMK", AutoSize = true, Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold), ForeColor = text, Location = new Point(28, 24) };
        var intro = new Label { Text = "Complete these checks once before playing.", AutoSize = true, ForeColor = dark ? Color.LightGray : Color.DimGray, Location = new Point(31, 65) };
        var closeApps = MakeStep("1", "Close the original GMK software and AntiMicro.", text, 105);
        var check = MakeButton("2   Check GMK connection", 150); check.Click += (_, _) => CheckDevice();
        var hide = MakeButton("3   Configure HidHide", 205); hide.Enabled = _hidHide.IsInstalled; hide.Click += (_, _) => ConfigureHidHide();
        var finish = MakeButton("4   Finish setup", 315); finish.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        _status.SetBounds(32, 260, 495, 42); _status.ForeColor = dark ? Color.FromArgb(156, 169, 188) : Color.DimGray; _status.Text = _hidHide.IsInstalled ? "Ready to check your device." : "HidHide is not installed. Movement can still work without it.";
        Controls.AddRange([title, intro, closeApps, check, hide, _status, finish]);
    }

    private void CheckDevice()
    {
        try { using var source = new GmkUsbSource(); source.Connect(); _status.Text = "✓ GMK connected and available."; _status.ForeColor = Color.FromArgb(22, 163, 74); }
        catch (Exception ex) { _status.Text = ex.Message; _status.ForeColor = Color.FromArgb(220, 38, 38); }
    }

    private void ConfigureHidHide()
    {
        try
        {
            var ids = _hidHide.FindGmkInstanceIds();
            if (ids.Count == 0) throw new InvalidOperationException("No connected GMK was found.");
            _hidHide.ConfigureForMapper(ids[0]);
            _status.Text = "✓ HidHide configured. Reconnect the GMK once after finishing."; _status.ForeColor = Color.FromArgb(22, 163, 74);
        }
        catch (Exception ex) { _status.Text = ex.Message; _status.ForeColor = Color.FromArgb(220, 38, 38); }
    }

    private static Label MakeStep(string number, string text, Color color, int top) => new() { Text = $"{number}   {text}", AutoSize = true, ForeColor = color, Font = new Font("Segoe UI Semibold", 10F), Location = new Point(32, top) };
    private static Button MakeButton(string text, int top) => new() { Text = text, Location = new Point(30, top), Size = new Size(500, 42), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White };
}
