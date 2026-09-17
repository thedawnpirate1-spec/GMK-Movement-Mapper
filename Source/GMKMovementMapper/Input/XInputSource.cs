using System.Runtime.InteropServices;

namespace GMKMovementMapper.Input;

/// <summary>
/// Reads a controller through the XInput API. Xbox 360 compatible gamepads
/// (including some GMK hardware revisions that expose VID 045E / PID 028E)
/// are XInput devices and do not appear through the legacy WinMM joystick
/// API (joyGetPosEx), so they must be read this way instead.
///
/// XInputGetState can block for a long time — or indefinitely — against an
/// unstable/flaky USB connection. All state here is static (one instance for
/// the whole process, not per <see cref="GmkUsbSource"/> connection attempt):
/// every query goes through a single outstanding background task per slot,
/// never a fresh one per call or per reconnect, with a short bounded wait for
/// it to finish. A slot that is genuinely stuck adds at most that bounded
/// wait to each call and leaks exactly one idle thread for the lifetime of
/// the app, no matter how many times the caller reconnects — it can never
/// accumulate more, so the UI thread can call this safely (e.g. on
/// "Start movement", repeated every second by auto-reconnect) without risking
/// a freeze or exhausting the thread pool.
/// </summary>
public static class XInputSource
{
    private const int ErrorSuccess = 0;
    private const int ErrorNotConnected = 1167; // ERROR_DEVICE_NOT_CONNECTED, used as the "no data yet" default too
    private const int GamepadA = 0x1000;
    private const int SlotCount = 4;
    private static readonly TimeSpan PollWait = TimeSpan.FromMilliseconds(300);

    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint PacketNumber;
        public Gamepad Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState14(uint userIndex, ref State state);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState910(uint userIndex, ref State state);

    private static int GetStateDirect(uint userIndex, ref State state)
    {
        try { return XInputGetState14(userIndex, ref state); }
        catch (DllNotFoundException) { return XInputGetState910(userIndex, ref state); }
    }

    // Even with single-flighting, a caller that polls faster than this (the read
    // loop calls TryRead as fast as it can, throttled elsewhere to ~250 Hz) would
    // still spawn a fresh Task.Run almost every call once the previous one
    // finishes, since XInputGetState usually returns in well under a millisecond.
    // This floor caps how often a new native call is even attempted per slot,
    // regardless of caller frequency.
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromMilliseconds(4);

    private static readonly object Lock = new();
    private static readonly Task?[] InFlight = new Task?[SlotCount];
    private static readonly int[] CachedError = [ErrorNotConnected, ErrorNotConnected, ErrorNotConnected, ErrorNotConnected];
    private static readonly State[] CachedState = new State[SlotCount];
    private static readonly DateTime[] LastUpdated = new DateTime[SlotCount];

    /// <summary>
    /// Ensures at most one background query is outstanding per slot for the
    /// whole process, waits a bounded time for it, and returns whatever is
    /// known — fresh if the query finished in time, the last cached value
    /// otherwise. Never spawns a second thread for a slot whose previous
    /// query hasn't returned yet, even across unrelated callers or reconnects,
    /// and never re-queries a slot more often than <see cref="MinRefreshInterval"/>.
    /// </summary>
    private static (int Error, State State) Poll(int index)
    {
        Task? inFlight;
        lock (Lock)
        {
            inFlight = InFlight[index];
            if (inFlight is null && DateTime.UtcNow - LastUpdated[index] >= MinRefreshInterval)
            {
                inFlight = InFlight[index] = Task.Run(() =>
                {
                    var state = new State();
                    var error = GetStateDirect((uint)index, ref state);
                    lock (Lock)
                    {
                        CachedError[index] = error;
                        CachedState[index] = state;
                        LastUpdated[index] = DateTime.UtcNow;
                        InFlight[index] = null;
                    }
                });
            }
        }

        inFlight?.Wait(PollWait);
        lock (Lock)
        {
            return (CachedError[index], CachedState[index]);
        }
    }

    /// <summary>Returns the first connected XInput controller slot (0-3), or null if none.</summary>
    public static int? FindConnected()
    {
        for (var i = 0; i < SlotCount; i++)
            if (Poll(i).Error == ErrorSuccess) return i;
        return null;
    }

    /// <summary>True if a controller currently answers on the given XInput slot (0-3).</summary>
    public static bool IsConnected(int index) => Poll(index).Error == ErrorSuccess;

    /// <summary>Connection state for every XInput slot, for a "which controller is my GMK" picker.</summary>
    public static IReadOnlyList<bool> GetSlotStatus()
    {
        var result = new bool[SlotCount];
        for (var i = 0; i < SlotCount; i++) result[i] = IsConnected(i);
        return result;
    }

    public static bool TryRead(int index, out double x, out double y, out bool aPressed)
    {
        var (error, state) = Poll(index);
        if (error != ErrorSuccess)
        {
            x = y = 0;
            aPressed = false;
            return false;
        }

        x = Math.Clamp(state.Gamepad.ThumbLX / 32767.0, -1.0, 1.0);
        y = Math.Clamp(state.Gamepad.ThumbLY / 32767.0, -1.0, 1.0);
        aPressed = (state.Gamepad.Buttons & GamepadA) != 0;
        return true;
    }
}
