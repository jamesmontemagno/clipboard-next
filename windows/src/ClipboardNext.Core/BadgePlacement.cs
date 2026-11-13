namespace ClipboardNext.Core;

/// <summary>A rectangle in physical screen pixels on the virtual desktop.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Intersects(PixelRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}

/// <summary>
/// Where the floating badge goes: the top-right corner of the primary work area by default,
/// or wherever the user dragged it, as long as that spot is still on a connected display.
/// </summary>
public static class BadgePlacement
{
    public const int WidthDip = 104;
    public const int HeightDip = 32;
    public const int MarginDip = 20;

    public static (int X, int Y) DefaultPosition(PixelRect workArea, int widthPx, int marginPx) =>
        (workArea.Right - widthPx - marginPx, workArea.Y + marginPx);

    public static bool IsOnScreen(PixelRect badge, IEnumerable<PixelRect> workAreas) =>
        workAreas.Any(area => area.Intersects(badge));

    /// <summary>
    /// The saved position when it is still visible on some display; otherwise the default,
    /// for example after the monitor the badge was on has been unplugged.
    /// </summary>
    public static (int X, int Y) Resolve(
        (int X, int Y)? saved,
        int widthPx,
        int heightPx,
        int marginPx,
        PixelRect primaryWorkArea,
        IEnumerable<PixelRect> workAreas)
    {
        if (saved is { } position && IsOnScreen(new PixelRect(position.X, position.Y, widthPx, heightPx), workAreas))
        {
            return position;
        }

        return DefaultPosition(primaryWorkArea, widthPx, marginPx);
    }
}
