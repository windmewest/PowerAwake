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
    private readonly CheckBox smoothDimming;
    private readonly TrackBar smoothDimmingDuration;
    private readonly Label smoothDimmingDurationValue;
    private readonly CheckBox applyToAc;
    private readonly CheckBox applyToDc;
    private readonly ComboBox language;
    private readonly Label errorLabel;
    private readonly AppSettings initialSettings;
    private readonly Action<AppSettings> save;
    private readonly Panel scrollHost;
    private readonly TableLayoutPanel mainTable;

    public SettingsForm(AppSettings settings, AppText text, Action<AppSettings> save)
    {
        initialSettings = settings;
        this.save = save;
        Text = text.SettingsTitle;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;

        // Scale the window to the current monitor's working area so it fits on small or high-DPI screens.
        var workingArea = (Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen)!.WorkingArea;
        MinimumSize = new Size(Math.Min(420, workingArea.Width - 40), Math.Min(360, workingArea.Height - 40));
        Size = new Size(Math.Min(620, (int)(workingArea.Width * 0.6)), Math.Min(760, (int)(workingArea.Height * 0.85)));

        sleep = new TimeoutEditor(settings.NormalSleepSeconds, CreateMinuteOptions(180), text);
        hibernate = new TimeoutEditor(settings.NormalHibernateSeconds, CreateMinuteOptions(720), text);
        dim = new TimeoutEditor(settings.DisplayDimSeconds, new[] { 0u, 5u, 10u, 15u, 30u, 60u, 120u, 180u, 300u, 600u, 900u, 1800u, 2700u, 3600u }, text);
        displayOff = new TimeoutEditor(settings.DisplayOffSeconds, new[] { 0u, 60u, 120u, 180u, 300u, 600u, 900u, 1800u, 2700u, 3600u }, text);
        brightness = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10, Value = settings.DimBrightnessPercent };
        brightnessValue = new Label { Text = $"{brightness.Value}%", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        brightness.ValueChanged += (_, _) => brightnessValue.Text = $"{brightness.Value}%";
        smoothDimming = new CheckBox { Text = text.SmoothDimming, Checked = settings.SmoothDimmingEnabled, AutoSize = true, TabStop = true };
        var durationTicks = (int)Math.Min(50, Math.Max(3, settings.SmoothDimmingDurationMs / 100));
        smoothDimmingDuration = new TrackBar { Minimum = 3, Maximum = 50, TickFrequency = 5, Value = durationTicks };
        smoothDimmingDurationValue = new Label { Text = FormatDuration(smoothDimmingDuration.Value), AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        smoothDimmingDuration.ValueChanged += (_, _) => smoothDimmingDurationValue.Text = FormatDuration(smoothDimmingDuration.Value);
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

        scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        mainTable = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(16, 12, 16, 12)
        };
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddSection(text.SleepPolicy);
        AddRow(text.Sleep, sleep);
        AddRow(text.Hibernate, hibernate);
        AddSection(text.DisplayPolicy);
        AddRow(text.Dim, dim);
        AddRow(text.DisplayOff, displayOff);
        AddRow(text.DimBrightness, CreateBrightnessValueControl());
        AddFullWidthRow(smoothDimming);
        AddRow(text.SmoothDimmingDuration, CreateDurationValueControl());
        AddSection(text.Scope);
        AddFullWidthRow(CreateToggleRow());
        AddSection(text.Language);
        AddRow(text.Language, language);
        errorLabel = new Label { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(520, 0), Margin = new Padding(0, 10, 0, 0) };
        AddFullWidthRow(errorLabel);

        scrollHost.Controls.Add(mainTable);
        Controls.Add(buttonBar);
        Controls.Add(scrollHost);
        AcceptButton = apply;
        CancelButton = cancel;
        scrollHost.ClientSizeChanged += (_, _) => SyncTableWidth();
        Shown += (_, _) => SyncTableWidth();
    }

    private void SyncTableWidth()
    {
        // ClientSize already excludes the scrollbar width once AutoScroll shows one.
        mainTable.Width = Math.Max(300, scrollHost.ClientSize.Width);
    }

    private void AddSection(string title)
    {
        var rowIndex = mainTable.RowCount++;
        mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = CreateSection(title);
        mainTable.Controls.Add(label, 0, rowIndex);
        mainTable.SetColumnSpan(label, 2);
    }

    private void AddRow(string caption, Control value)
    {
        var rowIndex = mainTable.RowCount++;
        mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 8, 2) };
        value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        value.Margin = new Padding(0, 4, 0, 4);
        mainTable.Controls.Add(label, 0, rowIndex);
        mainTable.Controls.Add(value, 1, rowIndex);
    }

    private void AddFullWidthRow(Control control)
    {
        var rowIndex = mainTable.RowCount++;
        mainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 4, 0, 4);
        mainTable.Controls.Add(control, 0, rowIndex);
        mainTable.SetColumnSpan(control, 2);
    }

    private Control CreateSection(string text) => new Label
    {
        Text = text,
        Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
        AutoSize = true,
        Margin = new Padding(0, 12, 0, 4)
    };

    private Control CreateBrightnessValueControl()
    {
        var panel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        brightness.Dock = DockStyle.Fill;
        brightnessValue.Anchor = AnchorStyles.Left;
        panel.Controls.Add(brightness, 0, 0);
        panel.Controls.Add(brightnessValue, 1, 0);
        return panel;
    }

    private Control CreateDurationValueControl()
    {
        var panel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        smoothDimmingDuration.Dock = DockStyle.Fill;
        smoothDimmingDurationValue.Anchor = AnchorStyles.Left;
        panel.Controls.Add(smoothDimmingDuration, 0, 0);
        panel.Controls.Add(smoothDimmingDurationValue, 1, 0);
        return panel;
    }

    private static string FormatDuration(int ticks) => $"{ticks / 10.0:0.0}s";

    private Control CreateToggleRow()
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0, 2, 0, 2) };
        row.Controls.Add(applyToAc);
        row.Controls.Add(applyToDc);
        return row;
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
            SmoothDimmingEnabled = smoothDimming.Checked,
            SmoothDimmingDurationMs = (uint)(smoothDimmingDuration.Value * 100),
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
