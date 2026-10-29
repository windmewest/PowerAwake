using System.Drawing.Drawing2D;

namespace PowerAwake.App.Ui;

internal enum Glyph
{
    Power,
    Infinity,
    Timer,
    Monitor,
    Gear,
    Startup,
    Exit,
    Check,
    Moon,
    Snowflake,
    Sun,
    Battery,
    Plug,
    Globe,
    Info,
    Chevron
}

/// <summary>Hand-built vector glyphs (24x24 grid, line style) rendered in any colour for light and dark themes.</summary>
internal static class Glyphs
{
    public static Bitmap Render(Glyph glyph, Color color, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        Draw(g, glyph, color, new Rectangle(0, 0, size, size));
        return bitmap;
    }

    public static void Draw(Graphics g, Glyph glyph, Color color, Rectangle bounds)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TranslateTransform(bounds.X, bounds.Y);
        var scale = Math.Min(bounds.Width, bounds.Height) / 24f;
        g.ScaleTransform(scale, scale);

        using var pen = new Pen(color, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(color);

        switch (glyph)
        {
            case Glyph.Power:
                g.DrawArc(pen, 5, 5, 14, 14, -55, 290);
                g.DrawLine(pen, 12, 3, 12, 12);
                break;

            case Glyph.Infinity:
                using (var path = new GraphicsPath())
                {
                    path.AddBezier(3f, 12f, 5.5f, 5.5f, 10f, 5.5f, 12f, 12f);
                    path.AddBezier(12f, 12f, 14f, 18.5f, 18.5f, 18.5f, 21f, 12f);
                    path.AddBezier(21f, 12f, 18.5f, 5.5f, 14f, 5.5f, 12f, 12f);
                    path.AddBezier(12f, 12f, 10f, 18.5f, 5.5f, 18.5f, 3f, 12f);
                    g.DrawPath(pen, path);
                }
                break;

            case Glyph.Timer:
                g.DrawEllipse(pen, 4, 5, 16, 16);
                g.DrawLine(pen, 12, 9, 12, 13.5f);
                g.DrawLine(pen, 12, 13.5f, 14.8f, 15.2f);
                g.DrawLine(pen, 9.5f, 2.5f, 14.5f, 2.5f);
                break;

            case Glyph.Monitor:
                using (var screen = RoundedRect(3, 4, 18, 12, 2))
                {
                    g.DrawPath(pen, screen);
                }

                g.DrawLine(pen, 12, 16, 12, 20);
                g.DrawLine(pen, 8, 20, 16, 20);
                break;

            case Glyph.Gear:
                DrawGear(g, pen);
                break;

            case Glyph.Startup:
                g.DrawLine(pen, 12, 3.5f, 12, 14);
                g.DrawLine(pen, 7.5f, 9.5f, 12, 14);
                g.DrawLine(pen, 16.5f, 9.5f, 12, 14);
                g.DrawLine(pen, 5, 19, 19, 19);
                break;

            case Glyph.Exit:
                g.DrawLines(pen, new PointF[] { new(10, 4), new(5, 4), new(5, 20), new(10, 20) });
                g.DrawLine(pen, 9, 12, 20, 12);
                g.DrawLine(pen, 16, 8, 20, 12);
                g.DrawLine(pen, 16, 16, 20, 12);
                break;

            case Glyph.Check:
                g.DrawLines(pen, new PointF[] { new(5, 12.5f), new(10, 17.5f), new(19, 7) });
                break;

            case Glyph.Moon:
                using (var outer = new GraphicsPath())
                using (var inner = new GraphicsPath())
                {
                    outer.AddEllipse(4, 4, 16, 16);
                    inner.AddEllipse(10, 1, 14, 14);
                    g.SetClip(inner, CombineMode.Exclude);
                    g.DrawPath(pen, outer);
                    g.SetClip(outer, CombineMode.Replace);
                    g.DrawPath(pen, inner);
                    g.ResetClip();
                }
                break;

            case Glyph.Snowflake:
                for (var i = 0; i < 3; i++)
                {
                    var angle = Math.PI / 3 * i;
                    var dx = (float)(8.5 * Math.Cos(angle));
                    var dy = (float)(8.5 * Math.Sin(angle));
                    g.DrawLine(pen, 12 - dx, 12 - dy, 12 + dx, 12 + dy);
                }
                break;

            case Glyph.Sun:
                g.DrawEllipse(pen, 8, 8, 8, 8);
                for (var i = 0; i < 8; i++)
                {
                    var angle = Math.PI / 4 * i;
                    g.DrawLine(
                        pen,
                        12 + (float)(6.5 * Math.Cos(angle)),
                        12 + (float)(6.5 * Math.Sin(angle)),
                        12 + (float)(9 * Math.Cos(angle)),
                        12 + (float)(9 * Math.Sin(angle)));
                }
                break;

            case Glyph.Battery:
                using (var body = RoundedRect(3, 7, 16, 10, 2))
                {
                    g.DrawPath(pen, body);
                }

                g.DrawLine(pen, 21, 10.5f, 21, 13.5f);
                g.FillRectangle(fill, 5.5f, 9.5f, 7, 5);
                break;

            case Glyph.Plug:
                g.DrawLine(pen, 9, 3, 9, 7);
                g.DrawLine(pen, 15, 3, 15, 7);
                using (var plug = RoundedRect(6, 7, 12, 6, 2))
                {
                    g.DrawPath(pen, plug);
                }

                g.DrawArc(pen, 8, 8, 8, 9, 0, 180);
                g.DrawLine(pen, 12, 17, 12, 21);
                break;

            case Glyph.Globe:
                g.DrawEllipse(pen, 3, 3, 18, 18);
                g.DrawEllipse(pen, 8, 3, 8, 18);
                g.DrawLine(pen, 3, 12, 21, 12);
                break;

            case Glyph.Info:
                g.DrawEllipse(pen, 3, 3, 18, 18);
                g.DrawLine(pen, 12, 11, 12, 16.5f);
                g.FillEllipse(fill, 11, 6.7f, 2, 2);
                break;

            case Glyph.Chevron:
                g.DrawLines(pen, new PointF[] { new(7, 9.5f), new(12, 14.5f), new(17, 9.5f) });
                break;
        }

        g.Restore(state);
    }

    private static void DrawGear(Graphics g, Pen pen)
    {
        const int teeth = 8;
        var points = new List<PointF>();
        for (var i = 0; i < teeth; i++)
        {
            var center = (Math.PI * 2 / teeth) * i;
            var half = Math.PI * 2 / teeth / 2;
            foreach (var (offset, radius) in new[] { (-half * 0.95, 7.2), (-half * 0.42, 9.4), (half * 0.42, 9.4), (half * 0.95, 7.2) })
            {
                var angle = center + offset;
                points.Add(new PointF(12 + (float)(radius * Math.Cos(angle)), 12 + (float)(radius * Math.Sin(angle))));
            }
        }

        g.DrawPolygon(pen, points.ToArray());
        g.DrawEllipse(pen, 9, 9, 6, 6);
    }

    public static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        var d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
