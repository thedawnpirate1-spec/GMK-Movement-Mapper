using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using GMKMovementMapper.Mapping;

namespace GMKMovementMapper.Output;

public sealed class VirtualControllerOutput : IDisposable
{
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;
    private short _lastX;
    private short _lastY;
    private bool _lastButtonPressed;
    private ControllerButtonMapping _lastMapping;
    private bool _hasLastReport;

    public bool IsConnected => _controller is not null;

    public void Connect()
    {
        Dispose();
        _client = new ViGEmClient();
        _controller = _client.CreateXbox360Controller();
        _controller.AutoSubmitReport = false;
        _controller.Connect();
        _hasLastReport = false;
    }

    public void Apply(double x, double y, bool buttonPressed, ControllerButtonMapping mapping)
    {
        var controller = _controller;
        if (controller is null)
        {
            return;
        }

        var axisX = ToAxis(x);
        var axisY = ToAxis(y);
        if (_hasLastReport && axisX == _lastX && axisY == _lastY &&
            buttonPressed == _lastButtonPressed && mapping == _lastMapping)
        {
            return;
        }

        controller.SetAxisValue(Xbox360Axis.LeftThumbX, axisX);
        controller.SetAxisValue(Xbox360Axis.LeftThumbY, axisY);
        ReleaseMappedButtons(controller);
        controller.SetButtonState(MapButton(mapping), buttonPressed);
        controller.SubmitReport();

        _lastX = axisX;
        _lastY = axisY;
        _lastButtonPressed = buttonPressed;
        _lastMapping = mapping;
        _hasLastReport = true;
    }

    public void Dispose()
    {
        if (_controller is not null)
        {
            try
            {
                _controller.AutoSubmitReport = false;
                _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
                _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
                ReleaseMappedButtons(_controller);
                _controller.SubmitReport();
                _controller.Disconnect();
            }
            catch
            {
                // The device may already have disappeared during shutdown.
            }
        }

        _controller = null;
        _client?.Dispose();
        _client = null;
        _hasLastReport = false;
    }

    private static short ToAxis(double value)
    {
        value = Math.Clamp(value, -1.0, 1.0);
        return value >= 0
            ? (short)Math.Round(value * short.MaxValue)
            : (short)Math.Round(value * -short.MinValue);
    }

    private static Xbox360Button MapButton(ControllerButtonMapping mapping) => mapping switch
    {
        ControllerButtonMapping.B => Xbox360Button.B,
        ControllerButtonMapping.X => Xbox360Button.X,
        ControllerButtonMapping.Y => Xbox360Button.Y,
        ControllerButtonMapping.LeftShoulder => Xbox360Button.LeftShoulder,
        ControllerButtonMapping.RightShoulder => Xbox360Button.RightShoulder,
        ControllerButtonMapping.LeftThumb => Xbox360Button.LeftThumb,
        ControllerButtonMapping.RightThumb => Xbox360Button.RightThumb,
        ControllerButtonMapping.Back => Xbox360Button.Back,
        ControllerButtonMapping.Start => Xbox360Button.Start,
        _ => Xbox360Button.A
    };

    private static void ReleaseMappedButtons(IXbox360Controller controller)
    {
        controller.SetButtonState(Xbox360Button.A, false);
        controller.SetButtonState(Xbox360Button.B, false);
        controller.SetButtonState(Xbox360Button.X, false);
        controller.SetButtonState(Xbox360Button.Y, false);
        controller.SetButtonState(Xbox360Button.LeftShoulder, false);
        controller.SetButtonState(Xbox360Button.RightShoulder, false);
        controller.SetButtonState(Xbox360Button.LeftThumb, false);
        controller.SetButtonState(Xbox360Button.RightThumb, false);
        controller.SetButtonState(Xbox360Button.Back, false);
        controller.SetButtonState(Xbox360Button.Start, false);
    }
}
