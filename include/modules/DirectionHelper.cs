namespace Matrix.Modules;

public enum ArrowDirection
{
    Right,
    Up,
    Left,
    Down
}

public static class DirectionHelper
{
    public static ArrowDirection FromVector(float x, float y)
    {
        float deg = MathF.Atan2(y, x) * (180f / MathF.PI);
        return deg switch
        {
            >= -45f and <= 45f   => ArrowDirection.Right,
            > 45f and <= 135f    => ArrowDirection.Up,
            < -45f and >= -135f  => ArrowDirection.Down,
            _                     => ArrowDirection.Left
        };
    }
}
