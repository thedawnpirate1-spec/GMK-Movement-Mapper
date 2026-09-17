using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GMKMovementMapper.Controls;

public sealed class BrandLogo : Control
{
    public BrandLogo() { DoubleBuffered = true; Size = new Size(48, 48); }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); AppBrand.Draw(e.Graphics, ClientRectangle); }
}

public static class AppBrand
{
    public static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) Draw(graphics, new Rectangle(0, 0, 64, 64));
        var handle = bitmap.GetHicon();
        try { using var temporary = Icon.FromHandle(handle); return (Icon)temporary.Clone(); }
        finally { DestroyIcon(handle); }
    }

    public static void Draw(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Min(bounds.Width, bounds.Height) / 64f;
        graphics.TranslateTransform(bounds.Left, bounds.Top); graphics.ScaleTransform(scale, scale);
        using var shadow = new SolidBrush(Color.FromArgb(55, 0, 0, 0)); graphics.FillEllipse(shadow, 5, 7, 54, 54);
        using var baseBrush = new LinearGradientBrush(new Rectangle(4, 3, 56, 56), Color.FromArgb(45, 212, 191), Color.FromArgb(99, 102, 241), 35f);
        graphics.FillRoundedRectangle(baseBrush, new RectangleF(4, 3, 56, 56), 16);
        using var inner = new SolidBrush(Color.FromArgb(238, 15, 23, 42)); graphics.FillEllipse(inner, 13, 12, 38, 38);
        using var track = new Pen(Color.FromArgb(220, 255, 255, 255), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawArc(track, 18, 17, 28, 28, 35, 285);
        using var arrow = new Pen(Color.White, 4.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawLine(arrow, 23, 32, 43, 32); graphics.DrawLine(arrow, 37, 25, 44, 32); graphics.DrawLine(arrow, 37, 39, 44, 32);
        using var input = new SolidBrush(Color.FromArgb(74, 222, 128)); graphics.FillEllipse(input, 14, 27, 10, 10);
        graphics.ResetTransform();
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr handle);
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = new GraphicsPath(); var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90); path.AddArc(bounds.Right-diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right-diameter, bounds.Bottom-diameter, diameter, diameter, 0, 90); path.AddArc(bounds.Left, bounds.Bottom-diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); graphics.FillPath(brush, path);
    }
}
