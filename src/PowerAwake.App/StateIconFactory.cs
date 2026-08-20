using System.Drawing.Drawing2D;

namespace PowerAwake.App;

internal enum TrayIconState
{
    Off,
    Indefinite,
    Interval,
    Error
}

internal static class StateIconFactory
{
    private const int CanvasSize = 32;

    public static Icon Create(TrayIconState state)
    {
        using var bitmap = new Bitmap(CanvasSize, CanvasSize);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);

        var color = state switch
        {
            TrayIconState.Off => Color.FromArgb(118, 125, 135),
            TrayIconState.Indefinite => Color.FromArgb(0, 148, 104),
            TrayIconState.Interval => Color.FromArgb(218, 132, 0),
            TrayIconState.Error => Color.FromArgb(198, 48, 48),
            _ => Color.Gray
        };

        switch (state)
        {
            case TrayIconState.Off:
                DrawInfinity(graphics, color);
                using (var erase = new Pen(Color.Transparent, 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                using (var clear = new SolidBrush(Color.Transparent))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.DrawLine(erase, 13, 16, 19, 16);
                    graphics.CompositingMode = CompositingMode.SourceOver;
                }
                break;

            case TrayIconState.Indefinite:
                DrawInfinity(graphics, color);
                break;

            case TrayIconState.Interval:
                DrawInfinity(graphics, color);
                using (var dot = new SolidBrush(color))
                {
                    graphics.FillEllipse(dot, 20, 2, 9, 9);
                }
                using (var clock = new Pen(Color.White, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    graphics.DrawLine(clock, 24.5f, 4.5f, 24.5f, 6.6f);
                    graphics.DrawLine(clock, 24.5f, 6.6f, 26.2f, 7.5f);
                }
                break;

            case TrayIconState.Error:
                DrawInfinity(graphics, color);
                using (var white = new SolidBrush(Color.White))
                {
                    graphics.FillEllipse(white, 20, 2, 9, 9);
                    graphics.FillRectangle(white, 23.3f, 3.7f, 2.2f, 3.1f);
                    graphics.FillRectangle(white, 23.3f, 8f, 2.2f, 1.3f);
                }
                break;
        }

        var iconHandle = bitmap.GetHicon();
        using var icon = Icon.FromHandle(iconHandle);
        return (Icon)icon.Clone();
    }

    private static void DrawInfinity(Graphics graphics, Color color)
    {
        using var path = new GraphicsPath();
        path.AddBezier(4, 16, 7, 8, 12, 8, 16, 16);
        path.AddBezier(16, 16, 20, 24, 25, 24, 28, 16);
        path.AddBezier(28, 16, 25, 8, 20, 8, 16, 16);
        path.AddBezier(16, 16, 12, 24, 7, 24, 4, 16);
        using var pen = new Pen(color, 3.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawPath(pen, path);
    }
}
