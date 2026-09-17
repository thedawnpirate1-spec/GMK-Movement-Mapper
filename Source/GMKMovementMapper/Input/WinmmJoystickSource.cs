using System.Runtime.InteropServices;

namespace GMKMovementMapper.Input;

public sealed class WinmmJoystickSource
{
    private const uint JoyReturnAll = 0x000000FF;
    private const uint JoyNoError = 0;

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort Mid;
        public ushort Pid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string ProductName;
        public uint XMin;
        public uint XMax;
        public uint YMin;
        public uint YMax;
        public uint ZMin;
        public uint ZMax;
        public uint NumButtons;
        public uint PeriodMin;
        public uint PeriodMax;
        public uint RMin;
        public uint RMax;
        public uint UMin;
        public uint UMax;
        public uint VMin;
        public uint VMax;
        public uint Caps;
        public uint MaxAxes;
        public uint NumAxes;
        public uint MaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string RegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string OemVxD;
    }

    [DllImport("winmm.dll", EntryPoint = "joyGetNumDevs")]
    private static extern uint JoyGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern uint joyGetDevCaps(uint id, ref JoyCaps caps, uint size);

    [DllImport("winmm.dll")]
    private static extern uint joyGetPosEx(uint id, ref JoyInfoEx info);

    public IReadOnlyList<(int Index, string Name)> Enumerate()
    {
        var result = new List<(int, string)>();
        var count = Math.Min(JoyGetNumDevs(), 16u);
        for (uint i = 0; i < count; i++)
        {
            var caps = new JoyCaps();
            if (joyGetDevCaps(i, ref caps, (uint)Marshal.SizeOf<JoyCaps>()) == JoyNoError)
                result.Add(((int)i, string.IsNullOrWhiteSpace(caps.ProductName) ? $"Joystick {i}" : caps.ProductName));
        }
        return result;
    }

    public bool TryRead(int index, out double x, out double y)
    {
        var info = new JoyInfoEx { Size = (uint)Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnAll };
        if (joyGetPosEx((uint)index, ref info) != JoyNoError)
        {
            x = y = 0;
            return false;
        }

        x = info.X / 65535.0 * 2.0 - 1.0;
        // Windows joystick Y grows downward; the mapper treats positive Y as forward.
        y = -(info.Y / 65535.0 * 2.0 - 1.0);
        return true;
    }
}
