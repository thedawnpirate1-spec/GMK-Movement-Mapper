using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GMKMovementMapper.Mapping;

namespace GMKMovementMapper.Output;

public sealed class KeyboardOutput : IDisposable
{
    private readonly object _stateLock = new();
    private bool _forwardDown;
    private bool _leftDown;
    private bool _backwardDown;
    private bool _rightDown;
    private bool _actionDown;
    private Keys _actionKey = Keys.None;
    private Keys _forwardKey = Keys.O;
    private Keys _leftKey = Keys.K;
    private Keys _backwardKey = Keys.L;
    private Keys _rightKey = Keys.OemSemicolon;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    // INPUT's native union is 32 bytes on 64-bit Windows because MOUSEINPUT is
    // larger than KEYBDINPUT. Fixing the union size makes sizeof(INPUT) 40.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    public void Apply(MovementKeys desired, Keys forwardKey, Keys leftKey, Keys backwardKey, Keys rightKey)
    {
        lock (_stateLock)
        {
            SetMappedState(ref _forwardKey, ref _forwardDown, forwardKey, desired.Forward);
            SetMappedState(ref _leftKey, ref _leftDown, leftKey, desired.Left);
            SetMappedState(ref _backwardKey, ref _backwardDown, backwardKey, desired.Backward);
            SetMappedState(ref _rightKey, ref _rightDown, rightKey, desired.Right);
        }
    }

    private static void SetMappedState(ref Keys activeKey, ref bool current, Keys requestedKey, bool desired)
    {
        if (current && activeKey != requestedKey)
        {
            Send(activeKey, keyUp: true);
            current = false;
        }

        activeKey = requestedKey;
        SetState(activeKey, ref current, desired);
    }

    public void ApplyAction(Keys key, bool pressed)
    {
        lock (_stateLock)
        {
            if (_actionDown && _actionKey != key)
            {
                Send(_actionKey, keyUp: true);
                _actionDown = false;
            }

            _actionKey = key;
            if (_actionDown == pressed) return;
            Send(key, keyUp: !pressed);
            _actionDown = pressed;
        }
    }

    public void ReleaseAllInputs()
    {
        lock (_stateLock)
        {
            SetState(_forwardKey, ref _forwardDown, false);
            SetState(_leftKey, ref _leftDown, false);
            SetState(_backwardKey, ref _backwardDown, false);
            SetState(_rightKey, ref _rightDown, false);
            if (_actionDown)
            {
                Send(_actionKey, keyUp: true);
                _actionDown = false;
            }
        }
    }

    private static void SetState(Keys key, ref bool current, bool desired)
    {
        if (current == desired) return;
        Send(key, keyUp: !desired);
        current = desired;
    }

    private static void Send(Keys key, bool keyUp)
    {
        var scanCode = MapVirtualKey((uint)key, 0);
        var input = new Input
        {
            Type = 1,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = 0,
                    ScanCode = (ushort)scanCode,
                    Flags = 0x0008u | (keyUp ? 0x0002u : 0u),
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the generated keyboard input.");
    }

    public void Dispose() => ReleaseAllInputs();
}
