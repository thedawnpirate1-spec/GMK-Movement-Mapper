using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace GMKMovementMapper;

public sealed class CurveEditorForm : Form
{
    private readonly CurvePreview _preview = new() { Dock = DockStyle.Fill };
    private readonly TrackBar _curve = new() { Minimum = 35, Maximum = 300, TickFrequency = 25, Dock = DockStyle.Fill };
    private readonly Label _value = new() { AutoSize = true };
    public double Exponent => _curve.Value / 100.0;

    public CurveEditorForm(double exponent, bool dark)
    {
        Text = "Stick response curve"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 470);
        MinimumSize = new Size(520, 430); Font = new Font("Segoe UI", 9.5F); AutoScaleMode = AutoScaleMode.Dpi;
        var background = dark ? Color.FromArgb(11, 17, 27) : Color.FromArgb(242, 245, 249);
        var surface = dark ? Color.FromArgb(22, 30, 43) : Color.White;
        var text = dark ? Color.FromArgb(235, 241, 249) : Color.FromArgb(28, 37, 51);
        BackColor = background; ForeColor = text; _preview.Dark = dark;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), BackColor = surface, RowCount = 6, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(new Label { Text = "Stick response curve", AutoSize = true, ForeColor = text, Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold) }, 0, 0);
        root.Controls.Add(_preview, 0, 1); root.Controls.Add(_value, 0, 2); _value.ForeColor = text;
        _curve.Value = Math.Clamp((int)Math.Round(exponent * 100), _curve.Minimum, _curve.Maximum);
        _curve.ValueChanged += (_, _) => UpdatePreview(); root.Controls.Add(_curve, 0, 3);
        root.Controls.Add(new Label { Text = "Below 1.00 responds faster near centre. Above 1.00 gives finer control near centre. 1.00 is linear.", AutoSize = true, MaximumSize = new Size(500, 0), ForeColor = dark ? Color.FromArgb(156,169,188) : Color.FromArgb(98,108,125) }, 0, 4);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Apply curve", DialogResult = DialogResult.OK, Size = new Size(120, 36), BackColor = Color.FromArgb(37,99,235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var linear = new Button { Text = "Set linear", Size = new Size(100,36) }; linear.Click += (_, _) => _curve.Value = 100;
        buttons.Controls.Add(save); buttons.Controls.Add(linear); root.Controls.Add(buttons, 0, 5);
        Controls.Add(root); AcceptButton = save; UpdatePreview();
    }

    private void UpdatePreview() { _value.Text = $"Curve strength: {Exponent:0.00}"; _preview.Exponent = Exponent; _preview.Invalidate(); }

    private sealed class CurvePreview : Control
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public double Exponent { get; set; } = 1;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool Dark { get; set; }
        public CurvePreview() { DoubleBuffered = true; MinimumSize = new Size(300, 230); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(Dark ? Color.FromArgb(20,27,39) : Color.FromArgb(248,250,252));
            var r = new Rectangle(45, 25, Math.Max(10, Width - 75), Math.Max(10, Height - 55));
            using var grid = new Pen(Dark ? Color.FromArgb(60,255,255,255) : Color.FromArgb(40,20,30,50));
            e.Graphics.DrawLine(grid, r.Left, r.Bottom, r.Right, r.Bottom); e.Graphics.DrawLine(grid, r.Left, r.Bottom, r.Left, r.Top);
            using var linear = new Pen(Color.FromArgb(110,148,163,184), 1.5f) { DashStyle = DashStyle.Dash }; e.Graphics.DrawLine(linear, r.Left, r.Bottom, r.Right, r.Top);
            using var curve = new Pen(Color.FromArgb(37,99,235), 3f); PointF? last = null;
            for (var i=0;i<=100;i++) { var x=i/100.0; var y=Math.Pow(x,Exponent); var p=new PointF(r.Left+(float)x*r.Width,r.Bottom-(float)y*r.Height); if(last is { } old)e.Graphics.DrawLine(curve,old,p); last=p; }
        }
    }
}
