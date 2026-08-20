using PowerAwake.Core.Models;

namespace PowerAwake.App;

internal sealed class SettingsForm : Form
{
    private readonly TimeoutEditor sleep;
    private readonly TimeoutEditor hibernate;
    private readonly TimeoutEditor dim;
    private readonly TimeoutEditor displayOff;
    private readonly TrackBar brightness;
    private readonly Label brightnessValue;
    private readonly CheckBox applyToAc;
    private readonly CheckBox applyToDc;
    private readonly ComboBox language;
    private readonly Label errorLabel;
    private readonly AppSettings initialSettings;
    private readonly Action<AppSettings> save;
    private readonly FlowLayoutPanel content;

    public SettingsForm(AppSettings settings, AppText text, Action<AppSettings> save)
    {
        initialSettings = settings;
        this.save = save;
        Text = text.SettingsTitle;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(440, 460);
        Size = new Size(600, 620);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        sleep = new TimeoutEditor(settings.NormalSleepSeconds, CreateMinuteOptions(180), text);
        hibernate = new TimeoutEditor(settings.NormalHibernateSeconds, CreateMinuteOptions(720), text);
        dim = new TimeoutEditor(settings.DisplayDimSeconds, new[] { 0u, 30u, 60u, 120u, 180u, 300u, 600u, 900u, 1800u, 2700u, 3600u }, text);
        displayOff = new TimeoutEditor(settings.DisplayOffSeconds, new[] { 0u, 60u, 120u, 180u, 300u, 600u, 900u, 1800u, 2700u, 3600u }, text);
        brightness = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10, Value = settings.DimBrightnessPercent };
        brightnessValue = new Label { Text = $"{brightness.Value}%", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        brightness.ValueChanged += (_, _) => brightnessValue.Text = $"{brightness.Value}%";
        applyToAc = new CheckBox { Text = text.ApplyToAc, Checked = settings.ApplyToAc, AutoSize = true, TabStop = true };
        applyToDc = new CheckBox { Text = text.ApplyToDc, Checked = settings.ApplyToDc, AutoSize = true, TabStop = true };
        language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        var languageOptions = AppText.Languages
            .Select(option => option.Code == "system" ? option with { DisplayName = text.SystemDefault } : option)
            .ToArray();
        language.Items.AddRange(languageOptions.Cast<object>().ToArray());
        language.DisplayMember = nameof(LanguageOption.DisplayName);
        language.ValueMember = nameof(LanguageOption.Code);
        language.SelectedIndex = Math.Max(0, Array.FindIndex(languageOptions, option => option.Code == settings.LanguageCode));

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var cancel = new Button { Text = text.Cancel, DialogResult = DialogResult.Cancel, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var apply = new Button { Text = text.Apply, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        apply.Click += ApplySettings;
        buttonBar.Controls.Add(cancel);
        buttonBar.Controls.Add(apply);

        content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16, 12, 16, 12)
        };
        content.Controls.Add(CreateSection(text.SleepPolicy));
        content.Controls.Add(CreateRow(text.Sleep, sleep));
        content.Controls.Add(CreateRow(text.Hibernate, hibernate));
        content.Controls.Add(CreateSection(text.DisplayPolicy));
        content.Controls.Add(CreateRow(text.Dim, dim));
        content.Controls.Add(CreateRow(text.DisplayOff, displayOff));
        content.Controls.Add(CreateBrightnessRow(text));
        content.Controls.Add(CreateSection(text.Scope));
        content.Controls.Add(CreateToggleRow());
        content.Controls.Add(CreateSection(text.Language));
        content.Controls.Add(CreateRow(text.Language, language));
        errorLabel = new Label { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(520, 0), Margin = new Padding(0, 10, 0, 0) };
        content.Controls.Add(errorLabel);

        Controls.Add(content);
        Controls.Add(buttonBar);
        AcceptButton = apply;
        CancelButton = cancel;
        content.SizeChanged += (_, _) => ResizeContentRows();
        Shown += (_, _) => ResizeContentRows();
    }

    private Control CreateSection(string text) => new Label
    {
        Text = text,
        Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
        AutoSize = true,
        Margin = new Padding(0, 12, 0, 4)
    };

    private Control CreateRow(string caption, Control value)
    {
        var row = new TableLayoutPanel { ColumnCount = 2, Height = 42, Margin = new Padding(0, 2, 0, 2) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var label = new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) };
        value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(value, 1, 0);
        return row;
    }

    private Control CreateBrightnessRow(AppText text)
    {
        var row = new TableLayoutPanel { ColumnCount = 3, Height = 56, Margin = new Padding(0, 2, 0, 2) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        brightness.Dock = DockStyle.Fill;
        row.Controls.Add(new Label { Text = text.DimBrightness, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        row.Controls.Add(brightness, 1, 0);
        row.Controls.Add(brightnessValue, 2, 0);
        return row;
    }

    private Control CreateToggleRow()
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0, 2, 0, 2) };
        row.Controls.Add(applyToAc);
        row.Controls.Add(applyToDc);
        return row;
    }

    private void ResizeContentRows()
    {
        var width = Math.Max(360, content.ClientSize.Width - content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
        foreach (Control control in content.Controls)
        {
            if (control is TableLayoutPanel || control is FlowLayoutPanel)
            {
                control.Width = width;
            }
        }
    }

    private void ApplySettings(object? sender, EventArgs e)
    {
        var settings = initialSettings with
        {
            NormalSleepSeconds = sleep.GetSeconds(),
            NormalHibernateSeconds = hibernate.GetSeconds(),
            DisplayDimSeconds = dim.GetSeconds(),
            DisplayOffSeconds = displayOff.GetSeconds(),
            DimBrightnessPercent = (byte)brightness.Value,
            ApplyToAc = applyToAc.Checked,
            ApplyToDc = applyToDc.Checked,
            LanguageCode = ((LanguageOption)language.SelectedItem!).Code
        };
        var errors = SettingsValidation.Validate(settings);
        if (errors.Count > 0)
        {
            errorLabel.Text = string.Join(Environment.NewLine, errors);
            return;
        }

        try
        {
            save(settings);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            errorLabel.Text = exception.Message;
        }
    }

    private static uint[] CreateMinuteOptions(uint maximumMinutes) =>
        new[] { 0u, 1u, 2u, 3u, 5u, 10u, 15u, 30u, 45u, 60u, 90u, 120u, 180u, 240u, 360u, 480u, 600u, 720u }
            .Where(minutes => minutes <= maximumMinutes)
            .Select(minutes => minutes * 60)
            .ToArray();

    private sealed class TimeoutEditor : Panel
    {
        private readonly ComboBox selection = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        private readonly NumericUpDown customSeconds = new() { Minimum = 1, Maximum = 720 * 60 * 60, Width = 130, Enabled = false };
        private readonly uint[] options;

        public TimeoutEditor(uint seconds, uint[] options, AppText text)
        {
            this.options = options;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            selection.Items.AddRange(options.Select(seconds => Format(seconds, text)).Append(text.Custom).ToArray());
            var index = Array.IndexOf(options, seconds);
            selection.SelectedIndex = index >= 0 ? index : options.Length;
            customSeconds.Value = Math.Min(Math.Max(seconds, 1), (uint)customSeconds.Maximum);
            selection.SelectedIndexChanged += (_, _) => customSeconds.Enabled = selection.SelectedIndex == options.Length;
            var flow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Dock = DockStyle.Fill };
            flow.Controls.Add(selection);
            flow.Controls.Add(customSeconds);
            Controls.Add(flow);
        }

        public uint GetSeconds() => selection.SelectedIndex == options.Length
            ? (uint)customSeconds.Value
            : options[selection.SelectedIndex];

        private static string Format(uint seconds, AppText text) => seconds == 0 ? text.Never : seconds < 60 ? $"{seconds} s" : $"{seconds / 60} {text.Minutes}";
    }
}
