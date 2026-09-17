using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using GMKMovementMapper.Diagnostics;

namespace GMKMovementMapper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error("Unhandled exception", e.ExceptionObject as Exception);
        Application.ThreadException += (_, e) =>
            Logger.Error("UI thread exception", e.Exception);

        using var mutex = new Mutex(true, @"Local\GMKMapper.SingleInstance", out var firstInstance);
        if (!firstInstance) { ExistingWindow.Activate(); return; }

        Logger.Info("GMK Mapper starting");

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var form = new MainForm();
        try { Application.Run(form); }
        finally { form.SafeShutdown(); }

        Logger.Info("GMK Mapper exited normally");
    }

    private static class ExistingWindow
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string windowName);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        public static void Activate() { var window = FindWindow(null, "GMK Mapper"); if (window == IntPtr.Zero) return; ShowWindow(window, 9); SetForegroundWindow(window); }
    }
}
