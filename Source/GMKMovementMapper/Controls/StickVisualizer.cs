using System.Drawing.Drawing2D;
using GMKMovementMapper.Mapping;

namespace GMKMovementMapper.Controls;

public sealed class StickVisualizer : Control
{
    private double _x;
    private double _y;
    private double _deadzone = 0.12;
    private double _outerZone = 0.98;
    private MovementKeys _movement = MovementKeys.None;
    private bool _aPressed;
    private double? _analogX;
    private double? _analogY;
    private bool _showDiagonalRange;
    private double _diagonalWindow = 15;
    private double _diagonalStart;
    private double _rotation;
    private string? _emphasis;
    private RectangleF _stickCircle;
    private bool _draggingStart;
    private bool _zoneEditingEnabled;
    private Bitmap? _staticLayer;
    private bool _staticDirty = true;
    public event Action<double, double>? DiagonalZoneChanged;
    public void SetZoneEditing(bool enabled) { _zoneEditingEnabled = enabled; Cursor = enabled ? Cursors.Cross : Cursors.Default; Invalidate(); }

    public StickVisualizer()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(28, 31, 36);
        ForeColor = Color.White;
        MinimumSize = new Size(230, 230);
    }

    public void SetState(double x, double y, double deadzone, double outerZone, MovementKeys movement, bool aPressed, double? analogX = null, double? analogY = null)
    {
        _x = Math.Clamp(x, -1.0, 1.0);
        _y = Math.Clamp(y, -1.0, 1.0);
        var nextDeadzone = Math.Clamp(deadzone, 0.0, 1.0); var nextOuter = Math.Clamp(outerZone, 0.5, 1.0);
        if (Math.Abs(nextDeadzone - _deadzone) > 0.0001 || Math.Abs(nextOuter - _outerZone) > 0.0001) _staticDirty = true;
        _deadzone = nextDeadzone; _outerZone = nextOuter;
        _movement = movement;
        _aPressed = aPressed;
        _analogX = analogX;
        _analogY = analogY;
        Invalidate();
    }

    public void SetDarkMode(bool dark)
    {
        BackColor = dark ? Color.FromArgb(20, 27, 39) : Color.FromArgb(28, 31, 36);
        _staticDirty = true;
        Invalidate();
    }

    public void SetControllerOverlay(bool enabled, double startDegrees, double windowDegrees, double rotationDegrees)
    {
        _showDiagonalRange = enabled;
        _diagonalStart = Math.Clamp(startDegrees, 0, 89);
        _diagonalWindow = Math.Clamp(windowDegrees, 0, 90);
        _rotation = rotationDegrees;
        _staticDirty = true;
        Invalidate();
    }

    public void SetEmphasis(string? area) { _emphasis = area; _staticDirty = true; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var padding = 18f;
        var diameter = Math.Min(ClientSize.Width - padding * 2, ClientSize.Height - padding * 2);
        if (diameter <= 20) return;

        var left = (ClientSize.Width - diameter) / 2f;
        var top = padding;
        var circle = new RectangleF(left, top, diameter, diameter);
        _stickCircle = circle;
        var centerX = circle.Left + circle.Width / 2f;
        var centerY = circle.Top + circle.Height / 2f;
        var radius = diameter / 2f;

        EnsureStaticLayer(circle);
        if (_staticLayer is not null) e.Graphics.DrawImageUnscaled(_staticLayer, 0, 0);
        using var outputLine = new Pen(Color.FromArgb(70, 170, 255), 3f);
        using var rawLine = new Pen(Color.FromArgb(60, 230, 125), 3f);
        using var rawDot = new SolidBrush(Color.FromArgb(75, 240, 135));

        var outputX = _analogX ?? ((_movement.Right ? 1 : 0) - (_movement.Left ? 1 : 0));
        var outputY = _analogY ?? ((_movement.Forward ? 1 : 0) - (_movement.Backward ? 1 : 0));
        if (outputX != 0 || outputY != 0)
        {
            var length = Math.Sqrt(outputX * outputX + outputY * outputY);
            var scale = _analogX.HasValue ? Math.Min(1.0, length) : 1.0;
            var endX = centerX + (float)(outputX / length * radius * 0.82 * scale);
            var endY = centerY - (float)(outputY / length * radius * 0.82 * scale);
            e.Graphics.DrawLine(outputLine, centerX, centerY, endX, endY);
            using var outputDot = new SolidBrush(Color.FromArgb(90, 180, 255));
            e.Graphics.FillEllipse(outputDot, endX - 6, endY - 6, 12, 12);
        }

        var rawX = centerX + (float)(_x * radius);
        var rawY = centerY - (float)(_y * radius);
        e.Graphics.DrawLine(rawLine, centerX, centerY, rawX, rawY);
        e.Graphics.FillEllipse(rawDot, rawX - 7, rawY - 7, 14, 14);

    }

    protected override void OnResize(EventArgs e) { _staticDirty = true; base.OnResize(e); }

    private void EnsureStaticLayer(RectangleF circle)
    {
        if (!_staticDirty && _staticLayer?.Size == ClientSize) return;
        _staticLayer?.Dispose(); _staticLayer = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
        using var graphics = Graphics.FromImage(_staticLayer); graphics.SmoothingMode = SmoothingMode.AntiAlias; graphics.Clear(BackColor);
        var centerX = circle.Left + circle.Width / 2f; var centerY = circle.Top + circle.Height / 2f; var radius = circle.Width / 2f;
        using var boundary = new Pen(Color.FromArgb(210,220,230),2f); using var guide = new Pen(Color.FromArgb(65,210,220,230),1f);
        if (_showDiagonalRange)
        {
            using var fill = new SolidBrush(Color.FromArgb(_emphasis == "diagonal" ? 85 : 45,139,92,246));
            using var edge = new Pen(Color.FromArgb(210,167,139,250),_emphasis == "diagonal" ? 2.8f : 1.4f);
            var width=(float)Math.Min(_diagonalWindow,90-_diagonalStart); var middle=(float)(_diagonalStart+width/2.0);
            DrawForwardSector(graphics,circle,-middle-(float)_rotation,width/2f,fill,edge); DrawForwardSector(graphics,circle,-180f+middle-(float)_rotation,width/2f,fill,edge);
        }
        graphics.DrawEllipse(boundary,circle); graphics.DrawLine(guide,centerX-radius,centerY,centerX+radius,centerY); graphics.DrawLine(guide,centerX,centerY-radius,centerX,centerY+radius);
        for(var i=0;i<4;i++){var a=(22.5+i*45.0)*Math.PI/180.0;var dx=(float)(Math.Cos(a)*radius);var dy=(float)(Math.Sin(a)*radius);graphics.DrawLine(guide,centerX-dx,centerY-dy,centerX+dx,centerY+dy);}
        var dz=(float)(_deadzone*radius); using var dzFill=new SolidBrush(Color.FromArgb(_emphasis=="deadzone"?115:70,230,75,75)); using var dzLine=new Pen(Color.FromArgb(220,230,90,90),_emphasis=="deadzone"?3f:1.5f);
        graphics.FillEllipse(dzFill,centerX-dz,centerY-dz,dz*2,dz*2); graphics.DrawEllipse(dzLine,centerX-dz,centerY-dz,dz*2,dz*2);
        var outer=(float)(_outerZone*radius); using var outerLine=new Pen(Color.FromArgb(190,245,158,11),_emphasis=="outer"?3f:1.4f){DashStyle=DashStyle.Dash}; graphics.DrawEllipse(outerLine,centerX-outer,centerY-outer,outer*2,outer*2);
        _staticDirty=false;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (!_zoneEditingEnabled || e.Button != MouseButtons.Left) return;
        var angle = PointerSideAngle(e.Location); if (angle is null) return;
        _draggingStart = Math.Abs(angle.Value - _diagonalStart) <= Math.Abs(angle.Value - (_diagonalStart + _diagonalWindow));
        Capture = true; UpdateDraggedBoundary(angle.Value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!Capture || !_zoneEditingEnabled) return;
        var angle = PointerSideAngle(e.Location); if (angle is not null) UpdateDraggedBoundary(angle.Value);
    }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }

    private double? PointerSideAngle(Point point)
    {
        if (_stickCircle.Width <= 0) return null;
        var cx = _stickCircle.Left + _stickCircle.Width / 2; var cy = _stickCircle.Top + _stickCircle.Height / 2;
        var dx = point.X - cx; var forward = cy - point.Y; if (forward < 0 || dx == 0 && forward == 0) return null;
        return Math.Atan2(forward, Math.Abs(dx)) * 180.0 / Math.PI;
    }

    private void UpdateDraggedBoundary(double angle)
    {
        angle = Math.Clamp(angle, 0, 90);
        if (_draggingStart) { var end = _diagonalStart + _diagonalWindow; _diagonalStart = Math.Min(angle, end); _diagonalWindow = end - _diagonalStart; }
        else _diagonalWindow = Math.Max(0, angle - _diagonalStart);
        DiagonalZoneChanged?.Invoke(_diagonalStart, _diagonalWindow); Invalidate();
    }

    private static void DrawForwardSector(Graphics graphics, RectangleF circle, float centerAngle, float halfWidth, Brush fill, Pen edge)
    {
        var start = centerAngle - halfWidth;
        var sweep = halfWidth * 2;
        graphics.FillPie(fill, circle.X, circle.Y, circle.Width, circle.Height, start, sweep);
        var cx = circle.Left + circle.Width / 2f; var cy = circle.Top + circle.Height / 2f; var radius = circle.Width / 2f;
        foreach (var angle in new[] { start, start + sweep })
        {
            var radians = angle * Math.PI / 180.0;
            graphics.DrawLine(edge, cx, cy, cx + (float)Math.Cos(radians) * radius, cy + (float)Math.Sin(radians) * radius);
        }
    }
}
