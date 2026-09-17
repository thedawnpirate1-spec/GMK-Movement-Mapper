namespace GMKMovementMapper.Mapping;

public readonly record struct AnalogStickOutput(double X, double Y);

public sealed class AnalogStickMapper
{
    private readonly MovementProfile _profile;

    public AnalogStickMapper(MovementProfile profile) => _profile = profile;

    public AnalogStickOutput Map(double inputX, double inputY)
    {
        var x = Math.Clamp(inputX, -1.0, 1.0);
        var y = Math.Clamp(_profile.InvertY ? -inputY : inputY, -1.0, 1.0);
        var magnitude = Math.Min(1.0, Math.Sqrt(x * x + y * y));
        if (magnitude <= _profile.Deadzone) return new AnalogStickOutput(0, 0);

        var angle = Math.Atan2(y, x) - _profile.ControllerRotation * Math.PI / 180.0;
        x = magnitude * Math.Cos(angle);
        y = magnitude * Math.Sin(angle);

        var directionDegrees = (Math.Atan2(y, x) * 180.0 / Math.PI + 360.0) % 360.0;
        var sideAngle = directionDegrees <= 90 ? directionDegrees : 180.0 - directionDegrees;
        var zoneStart = Math.Clamp(_profile.ControllerDiagonalStart, 0.0, 89.0);
        var zoneEnd = Math.Min(90.0, zoneStart + Math.Clamp(_profile.ControllerDiagonalWindow, 0.0, 90.0));
        if (_profile.ControllerDiagonalLock && y > 0 && directionDegrees <= 180 && sideAngle >= zoneStart && sideAngle <= zoneEnd)
        {
            // Fortnite movement angles are measured outward from forward. A 71° target
            // therefore produces a stick vector 19° above the horizontal axis.
            var fromForward = Math.Clamp(_profile.ControllerDiagonalAngle, 0.0, 90.0) * Math.PI / 180.0;
            x = Math.Sign(x) * magnitude * Math.Sin(fromForward);
            y = Math.Sign(y) * magnitude * Math.Cos(fromForward);
        }

        var usable = Math.Clamp((magnitude - _profile.Deadzone) /
            Math.Max(0.001, _profile.MaxZone - _profile.Deadzone), 0.0, 1.0);
        var exponent = _profile.ControllerLinearResponse ? 1.0 : Math.Clamp(_profile.ControllerCurveExponent, 0.35, 3.0);
        var curved = Math.Pow(usable, exponent);
        var antiDeadzone = Math.Clamp(_profile.ControllerAntiDeadzone, 0.0, 0.95);
        var response = curved <= 0 ? 0 : antiDeadzone + (1.0 - antiDeadzone) * curved;
        var directionLength = Math.Max(0.001, Math.Sqrt(x * x + y * y));
        return new AnalogStickOutput(
            Math.Clamp(x / directionLength * response, -1.0, 1.0),
            Math.Clamp(y / directionLength * response, -1.0, 1.0));
    }
}
