using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace GMKMovementMapper.Input;

/// <summary>
/// Reads the GMK's native USB report using the same interface, endpoint and
/// packet layout as the original GMK DriverNet implementation. Some GMK
/// hardware revisions do not expose that native STM32 interface (0483) at
/// all and instead present the joystick as a standard Xbox 360 compatible
/// gamepad. That kind of device is an XInput device, not a legacy WinMM
/// joystick, so when the native device cannot be found this falls back to
/// XInput (no driver replacement required).
///
/// Both the native LibUsbDotNet bus scan and XInputGetState can block for a
/// long time — or indefinitely — when a flaky USB device is on the bus, even
/// one that isn't the GMK itself (the scan enumerates everything). MainForm
/// retries Connect() automatically several times a second while disconnected,
/// so a naive per-call timeout would still spawn a new stuck thread every
/// call. The native scan below is single-flighted with a cooldown (static,
/// shared across every GmkUsbSource instance) so a hung bus can only ever
/// leak about one thread per cooldown window, not one per retry.
/// </summary>
public sealed class GmkUsbSource : IDisposable
{
    private const int VendorId = 0x0483;
    private const int JoystickProductId = 0x5750;
    private const int JoystickL3ProductId = 0x5751;
    private const int InterfaceNumber = 0;
    private const int ReportLength = 13;

    private static readonly TimeSpan ScanWait = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan ScanCooldown = TimeSpan.FromSeconds(20);
    private static readonly object ScanLock = new();
    private static Task<NativeDevice?>? _scanTask;
    private static DateTime _lastScanAttempt = DateTime.MinValue;

    private sealed record NativeDevice(UsbContext Context, UsbDevice Device, UsbEndpointReader Reader);

    private UsbContext? _context;
    private UsbDevice? _device;
    private UsbEndpointReader? _reader;
    private int _xinputIndex = -1;
    private int _winmmIndex = -1;

    public string DeviceName { get; private set; } = "GMK USB joystick";
    public bool IsConnected => (_device?.IsOpen == true && _reader is not null) || _xinputIndex >= 0 || _winmmIndex >= 0;

    /// <param name="forcedXInputSlot">
    /// A specific XInput slot (0-3) to use for the Xbox-compatible fallback, or
    /// -1 to auto-pick the first connected slot. Only matters when the native
    /// USB device isn't found; only needed when another real XInput controller
    /// is connected at the same time as the GMK, which would otherwise make
    /// auto-detection ambiguous.
    /// </param>
    public void Connect(int forcedXInputSlot = -1)
    {
        Dispose();

        var native = TryOpenNativeDeviceBounded();
        if (native is not null)
        {
            _context = native.Context;
            _device = native.Device;
            _reader = native.Reader;
            DeviceName = $"GMK direct USB ({_device.VendorId:X4}:{_device.ProductId:X4})";
            return;
        }

        int? xinputSlot = forcedXInputSlot >= 0
            ? (XInputSource.IsConnected(forcedXInputSlot) ? forcedXInputSlot : null)
            : XInputSource.FindConnected();

        if (xinputSlot is not null)
        {
            _xinputIndex = xinputSlot.Value;
            DeviceName = $"GMK compatible joystick (Xbox 360 mode, XInput #{_xinputIndex})";
            return;
        }

        if (forcedXInputSlot >= 0)
            throw new InvalidOperationException($"The GMK USB device (0483:5750/5751) was not found, and the selected fallback controller slot ({forcedXInputSlot}) is not connected. Check the GMK controller slot setting or switch it back to Automatic.");

        // Third tier: some controllers are neither the GMK's native chip nor an
        // XInput-compatible clone, but still register as a plain DirectInput/
        // WinMM game controller. Try that before giving up entirely.
        var winmmSlot = WinmmJoystickSource.FindConnected();
        if (winmmSlot is not null)
        {
            _winmmIndex = winmmSlot.Value;
            DeviceName = $"GMK compatible joystick (generic mode, joystick #{_winmmIndex})";
            return;
        }

        throw new InvalidOperationException("The GMK USB device (0483:5750/5751) was not found, and no Xbox-compatible or generic fallback controller is connected either. Reconnect it and make sure the original GMK Driver and AntiMicro are closed.");
    }

    /// <summary>
    /// Runs the native bus scan through a single shared, cooldown-gated task so
    /// repeated Connect() calls (auto-reconnect can retry several times a
    /// second) never pile up more than one blocked scan thread at a time. If a
    /// scan is already outstanding or in cooldown after a recent timeout, this
    /// returns null immediately (falls through to XInput) instead of starting
    /// another one.
    /// </summary>
    private static NativeDevice? TryOpenNativeDeviceBounded()
    {
        Task<NativeDevice?>? task;
        lock (ScanLock)
        {
            if (_scanTask is null && DateTime.UtcNow - _lastScanAttempt > ScanCooldown)
            {
                _lastScanAttempt = DateTime.UtcNow;
                _scanTask = Task.Run(ScanOnce);
            }
            task = _scanTask;
        }

        if (task is null) return null;
        if (!task.Wait(ScanWait)) return null;

        lock (ScanLock) { _scanTask = null; }
        return task.Result;
    }

    private static NativeDevice? ScanOnce()
    {
        UsbContext? context = null;
        UsbDevice? device = null;
        try
        {
            context = new UsbContext();
            using var devices = context.List();
            var match = devices.FirstOrDefault(d =>
                d.VendorId == VendorId &&
                (d.ProductId == JoystickProductId || d.ProductId == JoystickL3ProductId));

            device = match?.Clone() as UsbDevice;
            if (device is null || !device.TryOpen())
            {
                device?.Dispose();
                context.Dispose();
                return null;
            }

            device.SetConfiguration(device.Configuration);
            device.ClaimInterface(InterfaceNumber);
            var reader = device.OpenEndpointReader((ReadEndpointID)0x81, ReportLength);
            return new NativeDevice(context, device, reader);
        }
        catch
        {
            device?.Dispose();
            context?.Dispose();
            return null;
        }
    }

    public bool TryRead(out double x, out double y, out bool aPressed)
    {
        x = y = 0;
        aPressed = false;

        if (_reader is not null)
        {
            var report = new byte[ReportLength];
            var error = _reader.Read(report, 0, out var bytesRead);
            if (error != Error.Success || bytesRead != ReportLength) return false;

            // Byte 0 is the report ID. The original driver copies bytes 1..12,
            // then reads little-endian left-X from payload 2..3 and left-Y from 4..5.
            var rawX = (short)(report[3] | (report[4] << 8));
            var rawY = (short)(report[5] | (report[6] << 8));
            aPressed = (report[2] & 0x10) != 0;
            x = Math.Clamp(rawX / 32768.0, -1.0, 1.0);
            y = Math.Clamp(-rawY / 32768.0, -1.0, 1.0);
            return true;
        }

        if (_xinputIndex >= 0)
        {
            var ok = XInputSource.TryRead(_xinputIndex, out x, out y, out aPressed);
            // Unlike the native endpoint reader above (a blocking USB read that
            // naturally paces itself to the device's own data rate), XInputGetState
            // returns immediately with whatever the current state is. Calling it in
            // MainForm's tight read loop with nothing pacing it spins a CPU core at
            // effectively unlimited speed. 250 Hz is far more than this joystick
            // needs, so a short sleep here keeps the loop's true bottleneck the same
            // as the native path instead of spinning.
            Thread.Sleep(4);
            return ok;
        }

        if (_winmmIndex >= 0)
        {
            // Same non-blocking-API caveat as the XInput branch above.
            var ok = WinmmJoystickSource.TryRead(_winmmIndex, out x, out y, out aPressed);
            Thread.Sleep(4);
            return ok;
        }

        return false;
    }

    public void Dispose()
    {
        _reader = null;

        if (_device is not null)
        {
            if (_device.IsOpen)
            {
                if (_device is IUsbDevice wholeDevice)
                    wholeDevice.ReleaseInterface(InterfaceNumber);
                _device.Close();
            }
            _device.Dispose();
            _device = null;
        }

        _context?.Dispose();
        _context = null;

        _xinputIndex = -1;
        _winmmIndex = -1;
    }
}
