using System.Runtime.InteropServices;

namespace GMKMovementMapper.Input;

/// <summary>
/// Third-tier fallback: reads a generic DirectInput/WinMM joystick. Some
/// controllers are neither the GMK's native STM32 interface (0483) nor an
/// XInput-compatible Xbox clone (045E:028E) — a plain HID game controller
/// still registers with the legacy joystick API. Only used when both other
/// tiers in <see cref="GmkUsbSource"/> fail to find anything.
///
/// Same safety pattern as <see cref="XInputSource"/> and for the same reason:
/// joyGetPosEx is a non-blocking "state right now" call against USB hardware
/// that can still hang against a flaky device, and MainForm retries Connect()
/// several times a second while disconnected. Static, single-flighted per
/// slot, bounded wait — a stuck slot can never accumulate more than one
/// abandoned thread for the app's lifetime.
/// </summary>
public static class WinmmJoystickSource
{
    private const uint JoyReturnAll = 0x000000FF;
    private const uint JoyNoError = 0;
    private const int SlotCount = 16;
    private static readonly TimeSpan PollWait = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromMilliseconds(4);

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public uint Size;
        public uint Flags;
        public uint X;
        public uint Y;
        public uint Z;
        public uint R;
        public uint U;
        public uint V;
        public uint Buttons;
        public uint ButtonNumber;
        public uint Pov;
        public uint Reserved1;
        public uint Reserved2;
    }

    [DllImport("winmm.dll", EntryPoint = "joyGetNumDevs")]
    private static extern uint JoyGetNumDevs();

    [DllImport("winmm.dll")]
    private static extern uint joyGetPosEx(uint id, ref JoyInfoEx info);

    private static readonly object Lock = new();
    private static readonly Task?[] InFlight = new Task?[SlotCount];
    private static readonly bool[] CachedConnected = new bool[SlotCount];
    private static readonly JoyInfoEx[] CachedInfo = new JoyInfoEx[SlotCount];
    private static readonly DateTime[] LastUpdated = new DateTime[SlotCount];

    private static (bool Connected, JoyInfoEx Info) Poll(int index)
    {
        Task? inFlight;
        lock (Lock)
        {
            inFlight = InFlight[index];
            if (inFlight is null && DateTime.UtcNow - LastUpdated[index] >= MinRefreshInterval)
            {
                inFlight = InFlight[index] = Task.Run(() =>
                {
                    var info = new JoyInfoEx { Size = (uint)Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnAll };
                    var connected = joyGetPosEx((uint)index, ref info) == JoyNoError;
                    lock (Lock)
                    {
                        CachedConnected[index] = connected;
                        CachedInfo[index] = info;
                        LastUpdated[index] = DateTime.UtcNow;
                        InFlight[index] = null;
                    }
                });
            }
        }

        inFlight?.Wait(PollWait);
        lock (Lock)
        {
            return (CachedConnected[index], CachedInfo[index]);
        }
    }

    /// <summary>Returns the first connected legacy joystick slot, or null if none.</summary>
    public static int? FindConnected()
    {
        var count = (int)Math.Min(JoyGetNumDevs(), SlotCount);
        for (var i = 0; i < count; i++)
            if (Poll(i).Connected) return i;
        return null;
    }

    public static bool TryRead(int index, out double x, out double y, out bool aPressed)
    {
        var (connected, info) = Poll(index);
        if (!connected)
        {
            x = y = 0;
            aPressed = false;
            return false;
        }

        x = info.X / 65535.0 * 2.0 - 1.0;
        // Windows joystick Y grows downward; the mapper treats positive Y as forward.
        y = -(info.Y / 65535.0 * 2.0 - 1.0);
        aPressed = (info.Buttons & 0x1) != 0;
        return true;
    }
}
