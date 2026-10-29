using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace PowerAwake.App.Ui;

internal interface IHeightForWidth
{
    int GetHeight(int width);
}

internal static class UiScale
{
    public static int S(this Control control, int value) => (int)Math.Round(value * control.DeviceDpi / 96.0);
}

/// <summary>Lays children out top to bottom at full width.</summary>
internal class VStack : Panel, IHeightForWidth
{
    public VStack()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public int Gap { get; set; }

    public Padding Inset { get; set; }

    public int GetHeight(int width) => Measure(width, apply: false);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        SuspendLayout();
        var height = Measure(ClientSize.Width, apply: true);
        if (Height != height && Dock != DockStyle.Fill)
        {
            Height = height;
        }

        ResumeLayout(false);
    }

    private int Measure(int width, bool apply)
    {
        var inner = Math.Max(0, width - Inset.Horizontal);
        var y = Inset.Top;
        var first = true;
        foreach (Control child in Controls)
        {
            if (!child.Visible)
            {
                continue;
            }

            if (!first)
            {
                y += Gap;
            }

            first = false;
            var h = child is IHeightForWidth sized ? sized.GetHeight(inner) : child.Height;
            if (apply)
            {
                child.SetBounds(Inset.Left, y, inner, h);
            }

            y += h;
        }

        return y + Inset.Bottom;
    }
}

internal sealed class Card : VStack
{
    private readonly Palette palette;

    public Card(Palette palette)
    {
        this.palette = palette;
        BackColor = palette.WindowBack;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Glyphs.RoundedRect(0.5f, 0.5f, Width - 1, Height - 1, this.S(4) + 0.5f);
        using var fill = new SolidBrush(palette.CardBack);
        using var pen = new Pen(palette.CardBorder);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }
}

/// <summary>Windows 11 Settings style row: icon, title, description and a trailing control.</summary>
internal sealed class SettingRow : Panel, IHeightForWidth
{
    private readonly Palette palette;
    private readonly Control? trailing;
    private readonly Glyph? icon;
    private readonly Font titleFont = Fonts.Create(9.75f);
    private readonly Font descriptionFont = Fonts.Create(8.5f);
    private string title;
    private string description;

    public SettingRow(Palette palette, Glyph? icon, string title, string description, Control? trailing)
    {
        this.palette = palette;
        this.icon = icon;
        this.title = title;
        this.description = description;
        this.trailing = trailing;
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        if (trailing is not null)
        {
            Controls.Add(trailing);
            trailing.SizeChanged += (_, _) => { cachedHeight = null; (Parent as Control)?.PerformLayout(); };
        }
    }

    public bool ShowDivider { get; set; }

    public string Title
    {
        get => title;
        set { title = value; cachedHeight = null; Invalidate(); }
    }

    public string Description
    {
        get => description;
        set { description = value; cachedHeight = null; Parent?.PerformLayout(); Invalidate(); }
    }

    private int LeftInset => this.S(16) + (icon is null ? 0 : this.S(20) + this.S(16));

    private int TrailingWidth => trailing is null ? 0 : trailing.Width + this.S(24);

    private (int Width, int Height)? cachedHeight;

    public int GetHeight(int width)
    {
        if (cachedHeight is { } cached && cached.Width == width)
        {
            return cached.Height;
        }

        var textWidth = Math.Max(this.S(80), width - LeftInset - TrailingWidth - this.S(8));
        var titleSize = TextRenderer.MeasureText(title, titleFont, new Size(textWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        var height = titleSize.Height;
        if (!string.IsNullOrEmpty(description))
        {
            height += TextRenderer.MeasureText(description, descriptionFont, new Size(textWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + this.S(2);
        }

        var trailingHeight = trailing?.Height ?? 0;
        var result = Math.Max(this.S(56), Math.Max(height, trailingHeight) + this.S(24));
        cachedHeight = (width, result);
        return result;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (trailing is not null)
        {
            trailing.Location = new Point(Width - trailing.Width - this.S(16), (Height - trailing.Height) / 2);
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        if (ShowDivider)
        {
            using var pen = new Pen(palette.Divider);
            g.DrawLine(pen, 1, 0, Width - 2, 0);
        }

        var textColor = Enabled ? palette.Text : palette.SubText;
        var subColor = palette.SubText;
        if (icon is { } glyph)
        {
            var size = this.S(20);
            Glyphs.Draw(g, glyph, Enabled ? palette.Text : palette.SubText, new Rectangle(this.S(16), (Height - size) / 2, size, size));
        }

        var textWidth = Math.Max(this.S(80), Width - LeftInset - TrailingWidth - this.S(8));
        var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.Top;
        var titleHeight = TextRenderer.MeasureText(title, titleFont, new Size(textWidth, 0), flags).Height;
        var descHeight = string.IsNullOrEmpty(description) ? 0 : TextRenderer.MeasureText(description, descriptionFont, new Size(textWidth, 0), flags).Height + this.S(2);
        var top = (Height - titleHeight - descHeight) / 2;
        TextRenderer.DrawText(g, title, titleFont, new Rectangle(LeftInset, top, textWidth, titleHeight), textColor, flags);
        if (descHeight > 0)
        {
            TextRenderer.DrawText(g, description, descriptionFont, new Rectangle(LeftInset, top + titleHeight + this.S(2), textWidth, descHeight - this.S(2)), subColor, flags);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            titleFont.Dispose();
            descriptionFont.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class SectionLabel : Control, IHeightForWidth
{
    private readonly Palette palette;
    private readonly Font font = Fonts.Create(10.5f, FontStyle.Bold);

    public SectionLabel(Palette palette, string text)
    {
        this.palette = palette;
        Text = text;
        DoubleBuffered = true;
        BackColor = palette.WindowBack;
    }

    public int GetHeight(int width) => TextRenderer.MeasureText("Ag", font).Height + this.S(14);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        TextRenderer.DrawText(e.Graphics, Text, font, new Rectangle(this.S(4), this.S(8), Width - this.S(4), Height - this.S(8)), palette.Text, TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            font.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class ToggleSwitch : Control
{
    private readonly Palette palette;
    private bool isChecked;

    public ToggleSwitch(Palette palette)
    {
        this.palette = palette;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(this.S(44), this.S(22));
    }

    public event EventHandler? CheckedChanged;

    [DefaultValue(false)]
    public bool Checked
    {
        get => isChecked;
        set
        {
            if (isChecked == value)
            {
                return;
            }

            isChecked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Focus();
        Checked = !Checked;
    }

    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space)
        {
            Checked = !Checked;
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var fade = Enabled ? 255 : 90;
        var track = new RectangleF(1, 1, Width - 3, Height - 3);
        using var trackPath = Glyphs.RoundedRect(track.X, track.Y, track.Width, track.Height, track.Height / 2);
        var thumbDiameter = track.Height * (isChecked ? 0.58f : 0.52f);
        var thumbY = track.Y + ((track.Height - thumbDiameter) / 2);
        float thumbX;
        if (isChecked)
        {
            using var fill = new SolidBrush(Color.FromArgb(fade, palette.Accent));
            g.FillPath(fill, trackPath);
            thumbX = track.Right - thumbDiameter - (track.Height * 0.21f);
            using var thumb = new SolidBrush(Color.FromArgb(fade, palette.AccentText));
            g.FillEllipse(thumb, thumbX, thumbY, thumbDiameter, thumbDiameter);
        }
        else
        {
            using var pen = new Pen(Color.FromArgb(fade, palette.TrackOff), 1.2f);
            g.DrawPath(pen, trackPath);
            thumbX = track.X + (track.Height * 0.24f);
            using var thumb = new SolidBrush(Color.FromArgb(fade, palette.TrackOff));
            g.FillEllipse(thumb, thumbX, thumbY, thumbDiameter, thumbDiameter);
        }

        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(palette.Text, 1.2f);
            using var focusPath = Glyphs.RoundedRect(0, 0, Width - 1, Height - 1, (Height - 1) / 2f);
            g.DrawPath(focus, focusPath);
        }
    }
}

internal sealed class FluentSlider : Control
{
    private readonly Palette palette;
    private int value;
    private bool dragging;

    public FluentSlider(Palette palette)
    {
        this.palette = palette;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(this.S(190), this.S(32));
    }

    public event EventHandler? ValueChanged;

    public int Minimum { get; set; }

    public int Maximum { get; set; } = 100;

    public int Value
    {
        get => value;
        set
        {
            var clamped = Math.Clamp(value, Minimum, Maximum);
            if (clamped == this.value)
            {
                return;
            }

            this.value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int Margin2 => this.S(10);

    private void SetFromX(int x)
    {
        var span = Math.Max(1, Width - (Margin2 * 2));
        var ratio = Math.Clamp((x - Margin2) / (double)span, 0, 1);
        Value = Minimum + (int)Math.Round((Maximum - Minimum) * ratio);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        dragging = true;
        SetFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging)
        {
            SetFromX(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragging = false;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = Math.Max(1, (Maximum - Minimum) / 20);
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down: Value -= step; e.Handled = true; break;
            case Keys.Right or Keys.Up: Value += step; e.Handled = true; break;
            case Keys.Home: Value = Minimum; e.Handled = true; break;
            case Keys.End: Value = Maximum; e.Handled = true; break;
        }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var fade = Enabled ? 255 : 90;
        var span = Math.Max(1, Width - (Margin2 * 2));
        var ratio = Maximum == Minimum ? 0 : (value - Minimum) / (double)(Maximum - Minimum);
        var x = Margin2 + (float)(span * ratio);
        var cy = Height / 2f;
        var trackHeight = Math.Max(3f, this.S(4));

        using (var back = new SolidBrush(Color.FromArgb(fade, palette.ControlBorder)))
        using (var backPath = Glyphs.RoundedRect(Margin2, cy - (trackHeight / 2), span, trackHeight, trackHeight / 2))
        {
            g.FillPath(back, backPath);
        }

        if (x > Margin2)
        {
            using var front = new SolidBrush(Color.FromArgb(fade, palette.Accent));
            using var frontPath = Glyphs.RoundedRect(Margin2, cy - (trackHeight / 2), x - Margin2, trackHeight, trackHeight / 2);
            g.FillPath(front, frontPath);
        }

        var outer = this.S(20);
        var inner = dragging ? this.S(10) : this.S(12);
        using (var ring = new SolidBrush(Color.FromArgb(fade, palette.CardBack)))
        using (var ringPen = new Pen(Color.FromArgb(fade, palette.ControlBorder)))
        {
            g.FillEllipse(ring, x - (outer / 2f), cy - (outer / 2f), outer, outer);
            g.DrawEllipse(ringPen, x - (outer / 2f), cy - (outer / 2f), outer, outer);
        }

        using var dot = new SolidBrush(Color.FromArgb(fade, palette.Accent));
        g.FillEllipse(dot, x - (inner / 2f), cy - (inner / 2f), inner, inner);

        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(palette.Text, 1.2f);
            g.DrawEllipse(focus, x - (outer / 2f) - 2, cy - (outer / 2f) - 2, outer + 4, outer + 4);
        }
    }
}

internal sealed class DropdownBox : Control
{
    private readonly Palette palette;
    private int selectedIndex = -1;
    private bool hot;

    public DropdownBox(Palette palette, int width = 160)
    {
        this.palette = palette;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Fonts.Create(9.5f);
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(this.S(width), this.S(32));
    }

    public event EventHandler? SelectedIndexChanged;

    public List<string> Items { get; } = new();

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (selectedIndex == value)
            {
                return;
            }

            selectedIndex = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Down or Keys.Up or Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter or Keys.Down or Keys.Up)
        {
            ShowMenu();
            e.Handled = true;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Focus();
        ShowMenu();
    }

    private void ShowMenu()
    {
        if (Items.Count == 0)
        {
            return;
        }

        var menu = FluentMenuRenderer.CreateMenu(palette);
        menu.MinimumSize = new Size(Width, 0);
        for (var i = 0; i < Items.Count; i++)
        {
            var index = i;
            var item = new ToolStripMenuItem(Items[i])
            {
                Checked = i == selectedIndex,
                ForeColor = palette.Text,
                Padding = new Padding(2, this.S(4), 2, this.S(4)),
                Margin = new Padding(0, 1, 0, 1)
            };
            item.Click += (_, _) => SelectedIndex = index;
            menu.Items.Add(item);
        }

        menu.Closed += (_, _) => BeginInvoke(() => menu.Dispose());
        menu.Show(this, new Point(0, Height + 2));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var fade = Enabled ? 255 : 110;
        using var path = Glyphs.RoundedRect(0.5f, 0.5f, Width - 1, Height - 1, this.S(4));
        using var fill = new SolidBrush(Color.FromArgb(fade, hot && Enabled ? palette.ControlHover : palette.ControlBack));
        using var pen = new Pen(Focused && ShowFocusCues ? palette.Accent : Color.FromArgb(fade, palette.ControlBorder));
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
        var text = selectedIndex >= 0 && selectedIndex < Items.Count ? Items[selectedIndex] : string.Empty;
        var chevron = this.S(16);
        TextRenderer.DrawText(g, text, Font, new Rectangle(this.S(10), 0, Width - this.S(14) - chevron, Height), Enabled ? palette.Text : palette.SubText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        Glyphs.Draw(g, Glyph.Chevron, palette.SubText, new Rectangle(Width - chevron - this.S(8), (Height - chevron) / 2, chevron, chevron));
    }
}

internal sealed class FluentButton : Control, IButtonControl
{
    private readonly Palette palette;
    private bool hot;
    private bool pressed;
    private bool isDefault;

    public FluentButton(Palette palette, string text, bool primary)
    {
        this.palette = palette;
        Text = text;
        Primary = primary;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Font = Fonts.Create(9.5f);
        Cursor = Cursors.Hand;
        TabStop = true;
        BackColor = palette.WindowBack;
        Size = new Size(this.S(112), this.S(34));
    }

    public bool Primary { get; }

    public DialogResult DialogResult { get; set; }

    public void NotifyDefault(bool value)
    {
        isDefault = value;
        Invalidate();
    }

    public void PerformClick()
    {
        if (Enabled)
        {
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; pressed = false; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = true; Focus(); Invalidate(); }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            PerformClick();
            e.Handled = true;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (DialogResult != DialogResult.None && FindForm() is { } form)
        {
            form.DialogResult = DialogResult;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var fade = Enabled ? 255 : 100;
        Color back;
        Color fore;
        Color border;
        if (Primary)
        {
            var accent = palette.Accent;
            back = pressed ? Blend(accent, palette.IsDark ? Color.Black : Color.White, 0.25f) : hot ? Blend(accent, palette.IsDark ? Color.Black : Color.White, 0.12f) : accent;
            fore = palette.AccentText;
            border = back;
        }
        else
        {
            back = pressed ? palette.CardBack : hot ? palette.ControlHover : palette.ControlBack;
            fore = palette.Text;
            border = palette.ControlBorder;
        }

        using var path = Glyphs.RoundedRect(0.5f, 0.5f, Width - 1, Height - 1, this.S(4));
        using var fill = new SolidBrush(Color.FromArgb(fade, back));
        using var pen = new Pen(Color.FromArgb(fade, border));
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(palette.Text, 1.2f);
            using var focusPath = Glyphs.RoundedRect(2.5f, 2.5f, Width - 5, Height - 5, this.S(3));
            g.DrawPath(focus, focusPath);
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Color.FromArgb(fade, fore), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static Color Blend(Color a, Color b, float amount) => Color.FromArgb(
        a.R + (int)((b.R - a.R) * amount),
        a.G + (int)((b.G - a.G) * amount),
        a.B + (int)((b.B - a.B) * amount));
}

/// <summary>Single-line text input with a rounded Fluent frame and optional digits-only mode.</summary>
internal sealed class FluentTextBox : Panel
{
    private readonly Palette palette;
    private readonly TextBox input;

    public FluentTextBox(Palette palette, int width, bool digitsOnly)
    {
        this.palette = palette;
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Fonts.Create(9.5f);
        Size = new Size(this.S(width), this.S(32));
        input = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = palette.ControlBack,
            ForeColor = palette.Text,
            Font = Font,
            TextAlign = HorizontalAlignment.Left,
            MaxLength = 7
        };
        if (digitsOnly)
        {
            input.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar);
        }

        input.TextChanged += (_, _) => TextChanged2?.Invoke(this, EventArgs.Empty);
        input.GotFocus += (_, _) => Invalidate();
        input.LostFocus += (_, _) => Invalidate();
        Controls.Add(input);
    }

    public event EventHandler? TextChanged2;

    public string Value
    {
        get => input.Text;
        set => input.Text = value;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (input is null)
        {
            return;
        }

        var height = input.PreferredHeight;
        input.SetBounds(this.S(10), (Height - height) / 2, Math.Max(10, Width - this.S(20)), height);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        input.BackColor = Enabled ? palette.ControlBack : palette.WindowBack;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Glyphs.RoundedRect(0.5f, 0.5f, Width - 1, Height - 1, this.S(4));
        using var fill = new SolidBrush(Enabled ? palette.ControlBack : palette.WindowBack);
        using var pen = new Pen(input.Focused ? palette.Accent : palette.ControlBorder, input.Focused ? 1.5f : 1f);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }
}
