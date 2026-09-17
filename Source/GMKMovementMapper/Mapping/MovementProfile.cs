using System.Text.Json;

namespace GMKMovementMapper.Mapping;

public enum SnapMode
{
    Balanced,
    CardinalFirst,
    DiagonalFirst,
    Custom
}

public enum MovementOutputMode
{
    Keyboard,
    Controller
}

public enum ControllerButtonMapping
{
    A, B, X, Y, LeftShoulder, RightShoulder, LeftThumb, RightThumb, Back, Start
}

public enum AppearanceMode { Light, Dark, Black }

public sealed class MovementProfile
{
    public string Name { get; set; } = "Default";
    public double Deadzone { get; set; } = 0.12;
    public double MaxZone { get; set; } = 0.98;
    public SnapMode SnapMode { get; set; } = SnapMode.Custom;

    // Half-width of a diagonal sector. 22.5 is the mathematically balanced 8-way split.
    public double DiagonalSnapDegrees { get; set; } = 18.0;

    // Shifts the diagonal centers. Positive values make diagonals require more horizontal input.
    public double DiagonalBiasDegrees { get; set; } = 0.0;

    // WinmmJoystickSource already converts Windows' downward-growing Y axis to
    // "forward is positive". Leave this false for the GMK source.
    public bool InvertY { get; set; } = false;
    // WinForms Keys.Oemcomma. Stored as an integer to keep the profile portable.
    public int SprintKey { get; set; } = 188;
    public int ForwardKey { get; set; } = 87;       // W
    public int LeftKey { get; set; } = 65;          // A
    public int BackwardKey { get; set; } = 83;      // S
    public int RightKey { get; set; } = 68;         // D
    public bool DarkMode { get; set; }
    public AppearanceMode Appearance { get; set; } = AppearanceMode.Dark;
    public bool LowLatencyMode { get; set; }
    public bool SetupComplete { get; set; }
    public MovementOutputMode OutputMode { get; set; } = MovementOutputMode.Keyboard;
    public bool ControllerDiagonalLock { get; set; } = true;
    public double ControllerDiagonalAngle { get; set; } = 71.0;
    public double ControllerDiagonalWindow { get; set; } = 35.0;
    public double ControllerDiagonalStart { get; set; } = 0.0;
    public double ControllerRotation { get; set; }
    public bool ControllerLinearResponse { get; set; } = true;
    public double ControllerCurveExponent { get; set; } = 1.0;
    public double ControllerAntiDeadzone { get; set; } = 0.0;
    public ControllerButtonMapping ControllerAButton { get; set; } = ControllerButtonMapping.A;
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double MinX { get; set; } = -1;
    public double MaxX { get; set; } = 1;
    public double MinY { get; set; } = -1;
    public double MaxY { get; set; } = 1;
    public string TargetProcessName { get; set; } = "FortniteClient-Win64-Shipping";
    public int JoystickIndex { get; set; } = 0;
    public string? GmkInstanceId { get; set; }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GMKRebuild",
        "profile.json");

    public static MovementProfile Load()
    {
        try
        {
            if (File.Exists(DefaultPath))
            {
                var value = JsonSerializer.Deserialize<MovementProfile>(File.ReadAllText(DefaultPath));
                if (value is not null) return value;
            }
        }
        catch
        {
            // A bad profile should never prevent the mapper from starting.
        }

        return new MovementProfile();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath)!);
        File.WriteAllText(DefaultPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public (double X, double Y) Calibrate(double x, double y)
    {
        var calibratedX = x >= CenterX
            ? (x - CenterX) / Math.Max(0.05, MaxX - CenterX)
            : (x - CenterX) / Math.Max(0.05, CenterX - MinX);
        var calibratedY = y >= CenterY
            ? (y - CenterY) / Math.Max(0.05, MaxY - CenterY)
            : (y - CenterY) / Math.Max(0.05, CenterY - MinY);
        return (Math.Clamp(calibratedX, -1, 1), Math.Clamp(calibratedY, -1, 1));
    }

    public void Normalize()
    {
        Deadzone = FiniteClamp(Deadzone, 0, 0.8, 0.12);
        MaxZone = FiniteClamp(MaxZone, Math.Max(0.5, Deadzone + 0.05), 1, 0.98);
        ControllerDiagonalAngle = FiniteClamp(ControllerDiagonalAngle, 0, 90, 71);
        ControllerDiagonalStart = FiniteClamp(ControllerDiagonalStart, 0, 89, 0);
        ControllerDiagonalWindow = FiniteClamp(ControllerDiagonalWindow, 0, 90 - ControllerDiagonalStart, 20);
        ControllerRotation = FiniteClamp(ControllerRotation, -180, 180, 0);
        ControllerCurveExponent = FiniteClamp(ControllerCurveExponent, 0.35, 3, 1);
        ControllerAntiDeadzone = FiniteClamp(ControllerAntiDeadzone, 0, 0.95, 0);
        if (!Enum.IsDefined(Appearance)) Appearance = DarkMode ? AppearanceMode.Dark : AppearanceMode.Light;
        JoystickIndex = Math.Max(0, JoystickIndex);
    }

    private static double FiniteClamp(double value, double min, double max, double fallback)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
