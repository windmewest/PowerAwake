using PowerAwake.App.Ui;

namespace PowerAwake.App;

/// <summary>A timeout picker: common presets, or "Custom" revealing a number + unit entry.</summary>
internal sealed class TimeoutEditor : Panel
{
    private const uint MaximumSeconds = 720 * 60 * 60;

    private readonly uint[] options;
    private readonly DropdownBox preset;
    private readonly FluentTextBox amount;
    private readonly DropdownBox unit;

    public TimeoutEditor(Palette palette, uint seconds, uint[] options, AppText text, SettingsText extra)
    {
        this.options = options;
        DoubleBuffered = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;

        preset = new DropdownBox(palette, 150);
        foreach (var option in options)
        {
            preset.Items.Add(Format(option, text, extra));
        }

        preset.Items.Add(text.Custom);
        amount = new FluentTextBox(palette, 64, digitsOnly: true);
        unit = new DropdownBox(palette, 96);
        unit.Items.Add(extra.Seconds);
        unit.Items.Add(text.Minutes);

        var index = Array.IndexOf(options, seconds);
        if (index < 0)
        {
            var wholeMinutes = seconds % 60 == 0;
            amount.Value = (wholeMinutes ? seconds / 60 : seconds).ToString();
            unit.SelectedIndex = wholeMinutes ? 1 : 0;
            index = options.Length;
        }
        else
        {
            amount.Value = "10";
            unit.SelectedIndex = 1;
        }

        preset.SelectedIndex = index;
        preset.SelectedIndexChanged += (_, _) => OnValueChanged();
        amount.TextChanged2 += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        unit.SelectedIndexChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        Controls.Add(preset);
        Controls.Add(amount);
        Controls.Add(unit);
        ApplyLayout();
    }

    public event EventHandler? Changed;

    private bool IsCustom => preset.SelectedIndex == options.Length;

    public uint GetSeconds()
    {
        if (!IsCustom)
        {
            return options[preset.SelectedIndex];
        }

        _ = uint.TryParse(amount.Value, out var value);
        var total = (ulong)Math.Max(1u, value) * (unit.SelectedIndex == 1 ? 60UL : 1UL);
        return (uint)Math.Min(total, MaximumSeconds);
    }

    private void OnValueChanged()
    {
        ApplyLayout();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLayout()
    {
        var gap = this.S(8);
        preset.Location = new Point(0, 0);
        amount.Visible = unit.Visible = IsCustom;
        amount.Location = new Point(preset.Right + gap, 0);
        unit.Location = new Point(amount.Right + gap, 0);
        Size = new Size(IsCustom ? unit.Right : preset.Right, preset.Height);
    }

    private static string Format(uint seconds, AppText text, SettingsText extra) =>
        seconds == 0 ? text.Never
        : seconds < 60 ? $"{seconds} {extra.Seconds}"
        : seconds % 3600 == 0 && seconds >= 3600 ? $"{seconds / 3600} {extra.Hours}"
        : $"{seconds / 60} {text.Minutes}";
}
