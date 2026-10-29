using PowerAwake.App.Ui;
using PowerAwake.Core.Models;
using PowerAwake.Windows.Power;

namespace PowerAwake.App;

internal sealed class SettingsForm : Form
{
    private readonly Palette palette = Theme.Current;
    private readonly AppText text;
    private readonly SettingsText extra;
    private readonly AppSettings initialSettings;
    private readonly Action<AppSettings> save;
    private readonly bool hasBattery;

    private readonly TimeoutEditor sleep;
    private readonly TimeoutEditor hibernate;
    private readonly TimeoutEditor dim;
    private readonly TimeoutEditor displayOff;
    private readonly FluentSlider brightness = new(Theme.Current);
    private readonly Label brightnessValue;
    private readonly ToggleSwitch smoothDimming;
    private readonly FluentSlider duration = new(Theme.Current) { Minimum = 3, Maximum = 50 };
    private readonly Label durationValue;
    private readonly ToggleSwitch softwareDimming;
    private readonly ToggleSwitch applyToAc;
    private readonly ToggleSwitch applyToDc;
    private readonly DropdownBox language;
    private readonly LanguageOption[] languageOptions;

    private readonly SettingRow brightnessRow;
    private readonly SettingRow smoothRow;
    private readonly SettingRow durationRow;
    private readonly SettingRow softwareRow;
    private readonly SettingRow dimHintRow;
    private readonly Panel scrollHost;
    private readonly VStack content;
    private readonly Label message;
    private readonly FluentButton saveButton;
    private readonly FluentButton updateButton;
    private readonly SettingRow aboutRow;
    private readonly UpdateService updater = new();
    private readonly CancellationTokenSource updateCancellation = new();
    private UpdateInfo? pendingUpdate;
    private bool updateBusy;
    private bool syncingContent;

    /// <summary>True once an update installer has been started; the caller should exit the app.</summary>
    public bool UpdateLaunched { get; private set; }

    public SettingsForm(AppSettings settings, AppText text, DisplayCapabilities capabilities, Action<AppSettings> save)
    {
        this.text = text;
        this.save = save;
        DoubleBuffered = true;
        SuspendLayout();
        extra = SettingsText.For(settings.LanguageCode);
        hasBattery = SystemInformation.PowerStatus.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery
            && SystemInformation.PowerStatus.BatteryChargeStatus != BatteryChargeStatus.Unknown;
        initialSettings = hasBattery ? settings : settings with { ApplyToAc = true };

        Text = text.SettingsTitle;
        Font = Fonts.Create(9f);
        BackColor = palette.WindowBack;
        ForeColor = palette.Text;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowIcon = true;
        Icon = StateIconFactory.Create(TrayIconState.Indefinite, Theme.TaskbarUsesDark);

        var workingArea = (Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen)!.WorkingArea;
        MinimumSize = new Size(Math.Min(this.S(520), workingArea.Width - 40), Math.Min(this.S(420), workingArea.Height - 40));
        Size = new Size(Math.Min(this.S(760), workingArea.Width - 40), Math.Min(this.S(860), (int)(workingArea.Height * 0.92)));

        sleep = new TimeoutEditor(palette, settings.NormalSleepSeconds, CreateMinuteOptions(180), text, extra);
        hibernate = new TimeoutEditor(palette, settings.NormalHibernateSeconds, CreateMinuteOptions(720), text, extra);
        dim = new TimeoutEditor(palette, settings.DisplayDimSeconds, new[] { 0u, 15u, 30u, 60u, 120u, 180u, 300u, 600u, 900u, 1800u }, text, extra);
        displayOff = new TimeoutEditor(palette, settings.DisplayOffSeconds, new[] { 0u, 60u, 120u, 180u, 300u, 600u, 900u, 1800u, 2700u, 3600u }, text, extra);

        brightness.Value = settings.DimBrightnessPercent;
        brightnessValue = CreateValueLabel($"{brightness.Value}%");
        brightness.ValueChanged += (_, _) => brightnessValue.Text = $"{brightness.Value}%";
        duration.Value = (int)Math.Clamp(settings.SmoothDimmingDurationMs / 100, 3, 50);
        durationValue = CreateValueLabel(FormatDuration(duration.Value));
        duration.ValueChanged += (_, _) => durationValue.Text = FormatDuration(duration.Value);

        smoothDimming = new ToggleSwitch(palette) { Checked = settings.SmoothDimmingEnabled };
        softwareDimming = new ToggleSwitch(palette) { Checked = settings.AllowSoftwareDimming };
        applyToAc = new ToggleSwitch(palette) { Checked = settings.ApplyToAc };
        applyToDc = new ToggleSwitch(palette) { Checked = settings.ApplyToDc };

        languageOptions = AppText.Languages
            .Select(option => option.Code == "system" ? option with { DisplayName = text.SystemDefault } : option)
            .ToArray();
        language = new DropdownBox(palette, 170);
        language.Items.AddRange(languageOptions.Select(option => option.DisplayName));
        language.SelectedIndex = Math.Max(0, Array.FindIndex(languageOptions, option => option.Code == settings.LanguageCode));

        content = new VStack { Gap = this.S(8), Inset = new Padding(this.S(24), this.S(12), this.S(24), this.S(20)), BackColor = palette.WindowBack };
        scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = palette.WindowBack };
        scrollHost.Controls.Add(content);

        // Sleep
        var sleepCard = new Card(palette);
        AddRow(sleepCard, Glyph.Moon, text.Sleep, extra.SleepDesc, sleep);
        AddRow(sleepCard, Glyph.Snowflake, text.Hibernate, extra.HibernateDesc, hibernate);
        AddSection(text.SleepPolicy, sleepCard);

        // Display
        var displayCard = new Card(palette);
        AddRow(displayCard, Glyph.Sun, text.Dim, extra.DimDesc, dim);
        AddRow(displayCard, Glyph.Monitor, text.DisplayOff, extra.DisplayOffDesc, displayOff);
        brightnessRow = AddRow(displayCard, Glyph.Sun, text.DimBrightness, extra.DimBrightnessDesc, WithValue(brightness, brightnessValue));
        dimHintRow = AddRow(displayCard, Glyph.Info, extra.DimDisabledHint, string.Empty, null);
        AddSection(text.DisplayPolicy, displayCard);

        // Smooth dimming and the detected capabilities of this machine
        var smoothCard = new Card(palette);
        smoothRow = AddRow(smoothCard, Glyph.Sun, text.SmoothDimming, extra.SmoothDesc, smoothDimming);
        durationRow = AddRow(smoothCard, Glyph.Timer, text.SmoothDimmingDuration, extra.FadeDesc, WithValue(duration, durationValue));
        softwareRow = AddRow(smoothCard, Glyph.Monitor, extra.SoftwareDimming, extra.SoftwareDimmingDesc, softwareDimming);
        AddRow(smoothCard, Glyph.Info, extra.DetectedTitle, DescribeCapabilities(capabilities), null);
        AddSection(extra.SmoothSection, smoothCard);

        // Scope: a desktop has no battery, so the AC/DC choice is replaced by an explanation.
        var scopeCard = new Card(palette);
        if (hasBattery)
        {
            AddRow(scopeCard, Glyph.Plug, text.ApplyToAc, extra.ApplyAcDesc, applyToAc);
            AddRow(scopeCard, Glyph.Battery, text.ApplyToDc, extra.ApplyDcDesc, applyToDc);
        }
        else
        {
            AddRow(scopeCard, Glyph.Plug, text.ApplyToAc, extra.DesktopScopeNote, null);
        }

        AddSection(text.Scope, scopeCard);

        var languageCard = new Card(palette);
        AddRow(languageCard, Glyph.Globe, text.Language, extra.LanguageDesc, language);
        AddSection(text.Language, languageCard);

        var aboutCard = new Card(palette);
        updateButton = new FluentButton(palette, extra.CheckUpdate, primary: false) { BackColor = palette.CardBack, Width = this.S(150) };
        updateButton.Click += (_, _) => OnUpdateClick();
        aboutRow = AddRow(aboutCard, Glyph.Info, string.Format(extra.VersionTitle, UpdateService.CurrentVersion), extra.VersionDesc, updateButton);
        AddSection(extra.AboutSection, aboutCard);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = this.S(72), BackColor = palette.CardBack };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(palette.Divider);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };
        saveButton = new FluentButton(palette, text.Apply, primary: true) { Anchor = AnchorStyles.Right | AnchorStyles.Top };
        saveButton.BackColor = palette.CardBack;
        saveButton.Click += (_, _) => Commit();
        var cancel = new FluentButton(palette, text.Cancel, primary: false) { DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Right | AnchorStyles.Top, BackColor = palette.CardBack };
        message = new Label
        {
            AutoSize = false,
            ForeColor = palette.Danger,
            BackColor = palette.CardBack,
            Font = Fonts.Create(9f),
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        footer.Controls.Add(saveButton);
        footer.Controls.Add(cancel);
        footer.Controls.Add(message);
        footer.Resize += (_, _) => LayoutFooter(footer, saveButton, cancel);

        Controls.Add(scrollHost);
        Controls.Add(footer);
        AcceptButton = saveButton;
        CancelButton = cancel;
        LayoutFooter(footer, saveButton, cancel);

        foreach (var editor in new[] { sleep, hibernate, dim, displayOff })
        {
            editor.Changed += (_, _) => SyncState();
        }

        foreach (var toggle in new[] { smoothDimming, softwareDimming, applyToAc, applyToDc })
        {
            toggle.CheckedChanged += (_, _) => SyncState();
        }

        brightness.ValueChanged += (_, _) => SyncState();
        duration.ValueChanged += (_, _) => SyncState();
        language.SelectedIndexChanged += (_, _) => SyncState();

        scrollHost.ClientSizeChanged += (_, _) => SyncContent();
        content.SizeChanged += (_, _) => SyncContent();
        HandleCreated += (_, _) =>
        {
            NativeUi.ApplyWindowTheme(Handle, palette);
            NativeUi.ApplyControlTheme(scrollHost.Handle, palette);
        };
        Load += (_, _) => SyncContent();
        Shown += (_, _) =>
        {
            SyncContent();
            _ = CheckForUpdateAsync(silent: true);
        };
        FormClosed += (_, _) => updateCancellation.Cancel();
        SyncState();
        ResumeLayout(false);
    }

    private static Label CreateValueLabel(string value) => new()
    {
        Text = value,
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleRight,
        BackColor = Color.Transparent
    };

    private void LayoutFooter(Panel footer, Control saveBtn, Control cancel)
    {
        var margin = this.S(24);
        var gap = this.S(8);
        var top = (footer.Height - saveBtn.Height) / 2;
        saveBtn.Location = new Point(footer.Width - margin - saveBtn.Width, top);
        cancel.Location = new Point(saveBtn.Left - gap - cancel.Width, top);
        message.SetBounds(margin, top, Math.Max(0, cancel.Left - gap - margin), saveBtn.Height);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED: paint the whole child tree off-screen, no flicker
            return cp;
        }
    }

    private void SyncContent()
    {
        if (syncingContent)
        {
            return;
        }

        syncingContent = true;
        try
        {
            var width = scrollHost.ClientSize.Width;
            if (width > 0 && content.Width != width)
            {
                content.Width = width;
            }

            var min = new Size(0, content.Height);
            if (scrollHost.AutoScrollMinSize != min)
            {
                scrollHost.AutoScrollMinSize = min;
            }
        }
        finally
        {
            syncingContent = false;
        }
    }

    private Control WithValue(FluentSlider slider, Label value)
    {
        var panel = new Panel { BackColor = Color.Transparent, Size = new Size(this.S(260), this.S(32)) };
        slider.Location = new Point(0, 0);
        slider.Width = this.S(190);
        value.ForeColor = palette.SubText;
        value.SetBounds(slider.Right + this.S(6), 0, this.S(60), panel.Height);
        value.Font = Fonts.Create(9.5f);
        panel.Controls.Add(slider);
        panel.Controls.Add(value);
        return panel;
    }

    private SettingRow AddRow(Card card, Glyph icon, string title, string description, Control? trailing)
    {
        var row = new SettingRow(palette, icon, title, description, trailing) { ShowDivider = card.Controls.Count > 0 };
        card.Controls.Add(row);
        return row;
    }

    private void AddSection(string title, Card card)
    {
        content.Controls.Add(new SectionLabel(palette, title));
        content.Controls.Add(card);
    }

    private string DescribeCapabilities(DisplayCapabilities capabilities)
    {
        var parts = new List<string>();
        if (capabilities.InternalPanels > 0)
        {
            parts.Add(string.Format(extra.DetectInternal, capabilities.InternalPanels));
        }

        if (capabilities.DdcMonitors > 0)
        {
            parts.Add(string.Format(extra.DetectDdc, capabilities.DdcMonitors));
        }

        if (capabilities.SoftwareMonitors > 0)
        {
            parts.Add(string.Format(extra.DetectSoftware, capabilities.SoftwareMonitors));
        }

        if (parts.Count > 0)
        {
            return string.Join("  |  ", parts);
        }

        return initialSettings.AllowSoftwareDimming ? extra.DetectNone : extra.DetectNeedSoftware;
    }

    private static string FormatDuration(int ticks) => $"{ticks / 10.0:0.0}s";

    private AppSettings BuildSettings() => initialSettings with
    {
        NormalSleepSeconds = sleep.GetSeconds(),
        NormalHibernateSeconds = hibernate.GetSeconds(),
        DisplayDimSeconds = dim.GetSeconds(),
        DisplayOffSeconds = displayOff.GetSeconds(),
        DimBrightnessPercent = (byte)brightness.Value,
        SmoothDimmingEnabled = smoothDimming.Checked,
        SmoothDimmingDurationMs = (uint)(duration.Value * 100),
        AllowSoftwareDimming = softwareDimming.Checked,
        ApplyToAc = hasBattery ? applyToAc.Checked : true,
        ApplyToDc = applyToDc.Checked,
        LanguageCode = languageOptions[Math.Max(0, language.SelectedIndex)].Code
    };

    private List<string> Validate(AppSettings settings)
    {
        var errors = new List<string>();
        if (!settings.ApplyToAc && !settings.ApplyToDc)
        {
            errors.Add(extra.ErrNeedScope);
        }

        if (settings.DisplayDimSeconds != 0 && settings.DisplayOffSeconds != 0 && settings.DisplayOffSeconds <= settings.DisplayDimSeconds)
        {
            errors.Add(extra.ErrOffAfterDim);
        }

        return errors;
    }

    // Keeps dependent controls, validation feedback and the Apply button in sync with the current input.
    private void SyncState()
    {
        var dimNever = dim.GetSeconds() == 0;
        dimHintRow.Visible = dimNever;
        SetEnabled(brightnessRow, !dimNever);
        SetEnabled(smoothRow, !dimNever);
        SetEnabled(durationRow, !dimNever && smoothDimming.Checked);
        SetEnabled(softwareRow, !dimNever && smoothDimming.Checked);

        var current = BuildSettings();
        var errors = Validate(current);
        message.Text = string.Join("  ", errors);
        saveButton.Enabled = errors.Count == 0 && current != initialSettings;
    }

    private static void SetEnabled(Control control, bool enabled)
    {
        if (control.Enabled != enabled)
        {
            control.Enabled = enabled;
        }
    }

    private void OnUpdateClick()
    {
        if (updateBusy)
        {
            return;
        }

        _ = pendingUpdate is null ? CheckForUpdateAsync(silent: false) : InstallUpdateAsync(pendingUpdate);
    }

    private async Task CheckForUpdateAsync(bool silent)
    {
        if (updateBusy)
        {
            return;
        }

        updateBusy = true;
        updateButton.Enabled = false;
        if (!silent)
        {
            aboutRow.Description = extra.Checking;
        }

        try
        {
            pendingUpdate = await updater.CheckAsync(updateCancellation.Token);
            if (pendingUpdate is not null)
            {
                aboutRow.Description = string.Format(extra.NewVersion, pendingUpdate.Version);
                updateButton.Text = extra.UpdateNow;
            }
            else if (!silent)
            {
                aboutRow.Description = extra.UpToDate;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!silent)
            {
                aboutRow.Description = string.Format(extra.UpdateFailed, exception.Message);
            }
        }
        finally
        {
            updateBusy = false;
            if (!IsDisposed)
            {
                updateButton.Enabled = true;
            }
        }
    }

    private async Task InstallUpdateAsync(UpdateInfo update)
    {
        if (!UpdateService.IsInstalledCopy)
        {
            aboutRow.Description = extra.UpdatePortable;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(update.PageUrl) { UseShellExecute = true });
            return;
        }

        updateBusy = true;
        updateButton.Enabled = false;
        var progress = new Progress<int>(percent => aboutRow.Description = string.Format(extra.Downloading, percent));
        try
        {
            aboutRow.Description = string.Format(extra.Downloading, 0);
            await updater.DownloadAndStartAsync(update, progress, updateCancellation.Token);
            UpdateLaunched = true;
            Close();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            aboutRow.Description = string.Format(extra.UpdateFailed, exception.Message);
            updateBusy = false;
            if (!IsDisposed)
            {
                updateButton.Enabled = true;
            }
        }
    }

    private void Commit()
    {
        var settings = BuildSettings();
        var errors = Validate(settings);
        if (errors.Count > 0)
        {
            message.Text = string.Join("  ", errors);
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
            message.Text = exception.Message;
        }
    }

    private static uint[] CreateMinuteOptions(uint maximumMinutes) =>
        new[] { 0u, 1u, 2u, 3u, 5u, 10u, 15u, 30u, 45u, 60u, 90u, 120u, 180u, 240u, 360u, 480u, 600u, 720u }
            .Where(minutes => minutes <= maximumMinutes)
            .Select(minutes => minutes * 60)
            .ToArray();
}
