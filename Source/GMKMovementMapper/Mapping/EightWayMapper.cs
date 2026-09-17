namespace GMKMovementMapper.Mapping;

public readonly record struct StickSample(double X, double Y);

public readonly record struct MovementKeys(bool Forward, bool Left, bool Backward, bool Right)
{
    public static MovementKeys None => new(false, false, false, false);
}

public sealed class EightWayMapper
{
    private readonly MovementProfile _profile;

    public EightWayMapper(MovementProfile profile) => _profile = profile;

    public MovementKeys Map(StickSample input)
    {
        var x = Math.Clamp(input.X, -1.0, 1.0);
        var y = Math.Clamp(input.Y, -1.0, 1.0);
        if (_profile.InvertY) y = -y;

        var magnitude = Math.Sqrt(x * x + y * y);
        if (magnitude <= _profile.Deadzone) return MovementKeys.None;

        // Radial dead-zone compensation keeps the first usable movement responsive while
        // retaining a full-strength output at the edge of the stick.
        var usable = Math.Clamp((magnitude - _profile.Deadzone) /
                                Math.Max(0.001, _profile.MaxZone - _profile.Deadzone), 0.0, 1.0);
        if (usable <= 0.0) return MovementKeys.None;

        var angle = Math.Atan2(y, x) * 180.0 / Math.PI;
        if (angle < 0) angle += 360.0;

        var sector = SelectSector(angle);
        return sector switch
        {
            0 => new MovementKeys(false, false, false, true),   // east
            1 => new MovementKeys(true, false, false, true),    // north-east
            2 => new MovementKeys(true, false, false, false),   // north / W
            3 => new MovementKeys(true, true, false, false),    // north-west
            4 => new MovementKeys(false, true, false, false),   // west / A
            5 => new MovementKeys(false, true, true, false),    // south-west
            6 => new MovementKeys(false, false, true, false),   // south / S
            _ => new MovementKeys(false, false, true, true)     // south-east
        };
    }

    private int SelectSector(double angle)
    {
        // Fixed, equal 45-degree sectors: four cardinal directions and four diagonals.
        return NormalizeSector((int)Math.Floor((angle + 22.5) / 45.0));
    }

    private static int NormalizeSector(int value)
    {
        value %= 8;
        return value < 0 ? value + 8 : value;
    }

}
