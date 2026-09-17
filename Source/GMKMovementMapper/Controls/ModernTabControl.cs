namespace GMKMovementMapper.Controls;

public sealed class ModernTabControl : TabControl
{
    private bool _dark;
    private bool _black;

    public ModernTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SizeMode = TabSizeMode.Fixed; ItemSize = new Size(145, 38); Padding = new Point(18, 7);
    }

    public void SetTheme(bool dark, bool black) { _dark = dark; _black = black; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var background = _black ? Color.FromArgb(8,11,16) : _dark ? Color.FromArgb(22,30,43) : Color.FromArgb(248,250,252);
        var strip = _black ? Color.FromArgb(3,6,10) : _dark ? Color.FromArgb(15,22,33) : Color.FromArgb(226,232,240);
        var selected = _black ? Color.FromArgb(18,24,34) : _dark ? Color.FromArgb(35,45,61) : Color.White;
        var text = _dark ? Color.FromArgb(226,232,240) : Color.FromArgb(28,37,51);
        e.Graphics.Clear(background);
        using var stripBrush = new SolidBrush(strip); e.Graphics.FillRectangle(stripBrush, 0, 0, Width, ItemSize.Height + 4);
        for (var i=0; i<TabCount; i++)
        {
            var rect = GetTabRect(i); var active = i == SelectedIndex;
            using var fill = new SolidBrush(active ? selected : strip); e.Graphics.FillRectangle(fill, rect);
            TextRenderer.DrawText(e.Graphics, TabPages[i].Text, Font, rect, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (active) { using var accent = new SolidBrush(Color.FromArgb(59,130,246)); e.Graphics.FillRectangle(accent, rect.Left, rect.Bottom-3, rect.Width, 3); }
        }
        using var border = new Pen(_dark ? Color.FromArgb(55,65,81) : Color.FromArgb(203,213,225));
        e.Graphics.DrawRectangle(border, 0, ItemSize.Height+3, Width-1, Height-ItemSize.Height-4);
    }
}
