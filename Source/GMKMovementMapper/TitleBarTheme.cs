using System.Runtime.InteropServices;

namespace GMKMovementMapper;

internal static class TitleBarTheme
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    public static void Apply(Form form, bool dark)
    {
        if (!form.IsHandleCreated) return;
        var value = dark ? 1 : 0;
        try { DwmSetWindowAttribute(form.Handle, 20, ref value, sizeof(int)); }
        catch { }
    }
}
