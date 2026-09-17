using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace GMKMovementMapper.Input;

/// <summary>
/// Reads the GMK's native USB report using the same interface, endpoint and
/// packet layout as the original GMK DriverNet implementation.
/// </summary>
public sealed class GmkUsbSource : IDisposable
{
    private const int VendorId = 0x0483;
    private const int JoystickProductId = 0x5750;
    private const int JoystickL3ProductId = 0x5751;
    private const int InterfaceNumber = 0;
    private const int ReportLength = 13;

    private UsbContext? _context;
    private UsbDevice? _device;
    private UsbEndpointReader? _reader;

    public string DeviceName { get; private set; } = "GMK USB joystick";
    public bool IsConnected => _device?.IsOpen == true && _reader is not null;

    public void Connect()
    {
        Dispose();
        _context = new UsbContext();

        using var devices = _context.List();
        var match = devices.FirstOrDefault(device =>
            device.VendorId == VendorId &&
            (device.ProductId == JoystickProductId || device.ProductId == JoystickL3ProductId));

        _device = match?.Clone() as UsbDevice;
        if (_device is null)
            throw new InvalidOperationException("The GMK USB device (0483:5750/5751) was not found. Reconnect it and make sure the original GMK Driver and AntiMicro are closed.");

        if (!_device.TryOpen())
            throw new InvalidOperationException("The GMK was found but could not be opened. Close the original GMK Driver, reconnect the GMK, and try again.");

        _device.SetConfiguration(_device.Configuration);
        _device.ClaimInterface(InterfaceNumber);
        _reader = _device.OpenEndpointReader((ReadEndpointID)0x81, ReportLength);
        DeviceName = $"GMK direct USB ({_device.VendorId:X4}:{_device.ProductId:X4})";
    }

    public bool TryRead(out double x, out double y, out bool aPressed)
    {
        x = y = 0;
        aPressed = false;
        if (_reader is null) return false;

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
    }
}
