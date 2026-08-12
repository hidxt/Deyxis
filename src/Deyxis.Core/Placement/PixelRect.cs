namespace Deyxis.Core.Placement;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public long IntersectionArea(PixelRect other)
    {
        var left = Math.Max((long)X, other.X);
        var top = Math.Max((long)Y, other.Y);
        var right = Math.Min((long)X + Width, (long)other.X + other.Width);
        var bottom = Math.Min((long)Y + Height, (long)other.Y + other.Height);

        return Math.Max(0, right - left) * Math.Max(0, bottom - top);
    }
}
