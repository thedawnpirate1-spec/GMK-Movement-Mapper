namespace GMKMovementMapper.Diagnostics;

/// <summary>
/// Minimal rolling file logger. Writing to a file, not the console, is the
/// only way a crash or a silent connection failure is diagnosable after the
/// fact — a user reporting "it doesn't work" can send this file instead of
/// trying to reproduce the exact sequence of events for you.
/// </summary>
public static class Logger
{
    private static readonly object Lock = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GMKRebuild", "logs", $"gmk-mapper-{DateTime.Now:yyyy-MM-dd}.log");

    public static string CurrentLogPath => LogPath;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never be the reason the app crashes.
        }
    }
}
