using System.Drawing.Drawing2D;

namespace PowerAwake.App.Ui;

/// <summary>Windows 11 style context menu rendering: rounded hover pills, accent state indicator, themed colours.</summary>
internal sealed class FluentMenuRenderer : ToolStripProfessionalRenderer
{
    /// <summary>Marks a checkable item whose state is shown as a check mark on the right.</summary>
    public const string ToggleTag = "toggle";

    private readonly Palette palette;

    public FluentMenuRenderer(Palette palette) : base(new FluentColorTable(palette))
    {
        this.palette = palette;
        RoundedEdges = false;
    }

    public static ContextMenuStrip CreateMenu(Palette palette)
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new FluentMenuRenderer(palette),
            BackColor = palette.MenuBack,
            ForeColor = palette.Text,
            Font = Fonts.Create(9.5f),
            ShowImageMargin = true,
            ShowCheckMargin = false,
            ImageScalingSize = new Size(18, 18),
            Padding = new Padding(4),
            DropShadowEnabled = true
        };
        menu.HandleCreated += (_, _) => NativeUi.RoundCorners(menu.Handle);
        return menu;
    }

    public static void Style(ToolStripItem item, Palette palette)
    {
        item.ForeColor = palette.Text;
        item.Padding = new Padding(2, 7, 2, 7);
        item.Margin = new Padding(0, 1, 0, 1);
    }

    public static void StyleDropDown(ToolStripDropDownItem item, Palette palette)
    {
        if (item.DropDown is ToolStripDropDownMenu dropDown)
        {
            dropDown.Renderer = new FluentMenuRenderer(palette);
            dropDown.BackColor = palette.MenuBack;
            dropDown.Font = Fonts.Create(9.5f);
            dropDown.ShowImageMargin = false;
            dropDown.ShowCheckMargin = false;
            dropDown.Padding = new Padding(4);
            dropDown.HandleCreated += (_, _) => NativeUi.RoundCorners(dropDown.Handle);
        }
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(palette.MenuBack);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is not ToolStripDropDown)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(palette.MenuBorder);
        var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        if (!item.Selected && !(item is ToolStripMenuItem { DropDown.Visible: true }) || !item.Enabled)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Glyphs.RoundedRect(1.5f, 0.5f, item.Width - 3, item.Height - 1, 4);
        using var brush = new SolidBrush(palette.MenuHover);
        g.FillPath(brush, path);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var item = e.Item;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (item.Tag as string == ToggleTag)
        {
            var size = 16;
            var rect = new Rectangle(item.Width - size - 12, (item.Height - size) / 2, size, size);
            Glyphs.Draw(g, Glyph.Check, palette.Accent, rect);
            return;
        }

        using var pill = new SolidBrush(palette.Accent);
        var height = Math.Max(8, item.Height / 2 - 2);
        using var path = Glyphs.RoundedRect(1f, (item.Height - height) / 2f, 3f, height, 1.5f);
        g.FillPath(pill, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? e.Item.ForeColor : palette.SubText;
        e.TextFormat |= TextFormatFlags.EndEllipsis;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image is null)
        {
            return;
        }

        var item = e.Item;
        var size = item.Owner?.ImageScalingSize.Width ?? 18;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(e.Image, new Rectangle(e.ImageRectangle.X + ((e.ImageRectangle.Width - size) / 2), (item.Height - size) / 2, size, size));
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var cx = e.ArrowRectangle.X + (e.ArrowRectangle.Width / 2f);
        var cy = e.ArrowRectangle.Y + (e.ArrowRectangle.Height / 2f);
        using var pen = new Pen(palette.SubText, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLines(pen, new[] { new PointF(cx - 2, cy - 4), new PointF(cx + 2, cy), new PointF(cx - 2, cy + 4) });
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(palette.MenuBorder);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
    }

    private sealed class FluentColorTable : ProfessionalColorTable
    {
        private readonly Palette palette;

        public FluentColorTable(Palette palette)
        {
            this.palette = palette;
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground => palette.MenuBack;

        public override Color ImageMarginGradientBegin => palette.MenuBack;

        public override Color ImageMarginGradientMiddle => palette.MenuBack;

        public override Color ImageMarginGradientEnd => palette.MenuBack;

        public override Color MenuBorder => palette.MenuBorder;

        public override Color MenuItemBorder => Color.Transparent;

        public override Color MenuItemSelected => palette.MenuHover;
    }
}
