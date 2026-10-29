using PowerAwake.App.Ui;
using PowerAwake.Core.Models;
using PowerAwake.Core.Services;
using PowerAwake.Windows.Power;
using PowerAwake.Windows.Startup;
using Microsoft.Win32;

namespace PowerAwake.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon notifyIcon;
    private readonly AwakeController controller;
    private readonly SettingsService settingsService;
    private readonly WindowsStartupService startupService = new();
    private AppSettings settings;
    private readonly System.Windows.Forms.Timer intervalTimer;
    private readonly FileAppLogger logger;
    private Icon offIcon;
    private Icon indefiniteIcon;
    private Icon intervalIcon;
    private Icon errorIcon;
    private readonly SingleInstanceNotification instanceNotification;
    private readonly SynchronizationContext uiContext;
    private readonly SmoothDimmingService smoothDimmer = new();
    private readonly WindowsPowerPolicy powerPolicy = new();
    private readonly List<Image> menuImages = new();
    private ToolStripLabel? statusLabel;
    private AppText text = AppText.For("system");
    private SettingsText extraText = SettingsText.For("system");

    public TrayApplicationContext()
    {
        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerAwake");
        settingsService = new SettingsService(Path.Combine(dataDirectory, "settings.json"));
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        settings = settingsService.Load();
        text = AppText.For(settings.LanguageCode);
        extraText = SettingsText.For(settings.LanguageCode);
        logger = new FileAppLogger(Path.Combine(dataDirectory, "Logs"));
        logger.RemoveExpired(TimeSpan.FromDays(14));
        controller = new AwakeController(
            powerPolicy,
            new JsonFileStore<PowerSnapshot>(Path.Combine(dataDirectory, "recovery.json")),
            logger);
        controller.RecoverOnStartup();
        if (settings.StartWithWindows)
        {
            startupService.SetEnabled(Application.ExecutablePath, true);
        }

        (offIcon, indefiniteIcon, intervalIcon, errorIcon) = CreateIcons();
        Theme.Watch();
        Theme.Changed += OnThemeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        notifyIcon = new NotifyIcon
        {
            Icon = offIcon,
            Visible = true,
            Text = text.AwakeOff,
            ContextMenuStrip = CreateMenu()
        };
        notifyIcon.DoubleClick += (_, _) => ToggleIndefiniteAwake();
        instanceNotification = new SingleInstanceNotification(() => BeginInvokeOpenSettings());

        intervalTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        intervalTimer.Tick += (_, _) => CheckInterval();
        intervalTimer.Start();
        SystemEvents.SessionEnding += OnSessionEnding;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        smoothDimmer.Configure(GetEffectiveDimSeconds, () => settings.DimBrightnessPercent, () => settings.SmoothDimmingDurationMs, () => settings.AllowSoftwareDimming);
        smoothDimmer.Start();
        EnsureNativeDimDisabled();
    }

    private static (Icon Off, Icon Indefinite, Icon Interval, Icon Error) CreateIcons()
    {
        var dark = Theme.TaskbarUsesDark;
        return (
            StateIconFactory.Create(TrayIconState.Off, dark),
            StateIconFactory.Create(TrayIconState.Indefinite, dark),
            StateIconFactory.Create(TrayIconState.Interval, dark),
            StateIconFactory.Create(TrayIconState.Error, dark));
    }

    private void OnThemeChanged()
    {
        var previous = new[] { offIcon, indefiniteIcon, intervalIcon, errorIcon };
        (offIcon, indefiniteIcon, intervalIcon, errorIcon) = CreateIcons();
        RefreshLocalizedUi();
        foreach (var icon in previous)
        {
            icon.Dispose();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Monitors were added/removed or resized: re-detect on a worker so the UI thread never waits on WMI/DDC.
        _ = Task.Run(smoothDimmer.Refresh);
    }

    private uint GetEffectiveDimSeconds()
    {
        if (!settings.SmoothDimmingEnabled)
        {
            return 0;
        }

        // Keep screen on suppresses dimming entirely while Awake is active.
        if (controller.State is not AwakeState.Off && settings.KeepScreenOn)
        {
            return 0;
        }

        return settings.DisplayDimSeconds;
    }

    private void EnsureNativeDimDisabled()
    {
        // We animate the dim/undim ourselves; Windows' own dim timeout must stay off so it can't double-dim.
        if (!smoothDimmer.IsSupported || !settings.SmoothDimmingEnabled)
        {
            return;
        }

        try
        {
            var scheme = powerPolicy.GetActiveScheme();
            var values = powerPolicy.ReadValues(scheme);
            if (values.DimAc != 0 || values.DimDc != 0)
            {
                powerPolicy.WriteValues(scheme, values with { DimAc = 0, DimDc = 0 });
            }
        }
        catch (Exception exception)
        {
            logger.Error("接管屏幕变暗时间失败，回退到系统原生变暗。", exception);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            intervalTimer.Stop();
            intervalTimer.Dispose();
            smoothDimmer.Stop();
            smoothDimmer.Dispose();
            instanceNotification.Dispose();
            SystemEvents.SessionEnding -= OnSessionEnding;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            Theme.Changed -= OnThemeChanged;
            try
            {
                controller.StopAndApplyNormalSettings(settings);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"恢复电源策略失败：{exception.Message}", "powerAwake", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            offIcon.Dispose();
            indefiniteIcon.Dispose();
            intervalIcon.Dispose();
            errorIcon.Dispose();
            foreach (var image in menuImages)
            {
                image.Dispose();
            }

            menuImages.Clear();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip CreateMenu()
    {
        var palette = Theme.Current;
        var menu = FluentMenuRenderer.CreateMenu(palette);
        var iconSize = (int)Math.Round(18 * menu.DeviceDpi / 96.0);
        menu.ImageScalingSize = new Size(iconSize, iconSize);
        foreach (var image in menuImages)
        {
            image.Dispose();
        }

        menuImages.Clear();

        Image Img(Glyph glyph, Color? color = null)
        {
            var image = Glyphs.Render(glyph, color ?? palette.Text, iconSize);
            menuImages.Add(image);
            return image;
        }

        ToolStripMenuItem Item(string caption, Glyph glyph)
        {
            var item = new ToolStripMenuItem(caption) { Image = Img(glyph) };
            FluentMenuRenderer.Style(item, palette);
            return item;
        }

        var header = new ToolStripLabel("powerAwake")
        {
            Font = Fonts.Create(10f, FontStyle.Bold),
            ForeColor = palette.Text,
            Image = Img(Glyph.Infinity, palette.Accent),
            Padding = new Padding(2, 6, 2, 0)
        };

        statusLabel = new ToolStripLabel(text.AwakeOff)
        {
            Font = Fonts.Create(8.5f),
            ForeColor = palette.SubText,
            Padding = new Padding(2, 0, 2, 6)
        };

        var off = Item(text.AwakeOff, Glyph.Power);
        var indefinite = Item(text.KeepAwakeIndefinitely, Glyph.Infinity);
        var interval = Item(text.KeepAwakeInterval, Glyph.Timer);
        var keepScreenOn = Item(text.KeepScreenOn, Glyph.Monitor);
        keepScreenOn.CheckOnClick = true;
        keepScreenOn.Checked = settings.KeepScreenOn;
        keepScreenOn.Tag = FluentMenuRenderer.ToggleTag;
        var settingsItem = Item(text.Settings, Glyph.Gear);
        var startWithWindows = Item(text.StartWithWindows, Glyph.Startup);
        startWithWindows.CheckOnClick = true;
        startWithWindows.Checked = settings.StartWithWindows;
        startWithWindows.Tag = FluentMenuRenderer.ToggleTag;
        var exit = Item(text.Exit, Glyph.Exit);

        off.Click += (_, _) => RunAction(controller.Stop, () => ShowNotification(text.ClosedTitle, text.ClosedMessage));
        indefinite.Click += (_, _) => RunAction(() => controller.StartIndefinitely(settings), () => ShowNotification(text.OpenedTitle, text.OpenedIndefinite));
        AddIntervalOption(interval, $"15 {text.Minutes}", TimeSpan.FromMinutes(15), palette);
        AddIntervalOption(interval, $"30 {text.Minutes}", TimeSpan.FromMinutes(30), palette);
        AddIntervalOption(interval, $"1 {extraText.Hours}", TimeSpan.FromHours(1), palette);
        AddIntervalOption(interval, $"2 {extraText.Hours}", TimeSpan.FromHours(2), palette);
        var custom = new ToolStripMenuItem(text.Custom);
        custom.Click += (_, _) => StartCustomInterval();
        interval.DropDownItems.Add(custom);
        StyleSubItem(custom, palette);
        FluentMenuRenderer.StyleDropDown(interval, palette);
        keepScreenOn.CheckedChanged += (_, _) => RunAction(() =>
        {
            settings = settings with { KeepScreenOn = keepScreenOn.Checked };
            controller.SetKeepScreenOn(settings);
            settingsService.Save(settings);
        });
        startWithWindows.CheckedChanged += (_, _) => RunAction(() =>
        {
            startupService.SetEnabled(Application.ExecutablePath, startWithWindows.Checked);
            settings = settings with { StartWithWindows = startWithWindows.Checked };
            settingsService.Save(settings);
        });
        settingsItem.Click += (_, _) => OpenSettings();
        exit.Click += (_, _) => ExitThread();

        menu.Items.Add(header);
        menu.Items.Add(statusLabel);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(off);
        menu.Items.Add(indefinite);
        menu.Items.Add(interval);
        menu.Items.Add(keepScreenOn);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(startWithWindows);
        menu.Items.Add(exit);
        menu.Opening += (_, _) => UpdateMenu(off, indefinite, interval, keepScreenOn);
        return menu;
    }

    private void AddIntervalOption(ToolStripMenuItem parent, string caption, TimeSpan duration, Palette palette)
    {
        var option = new ToolStripMenuItem(caption);
        option.Click += (_, _) => StartInterval(duration);
        parent.DropDownItems.Add(option);
        StyleSubItem(option, palette);
    }

    private static void StyleSubItem(ToolStripMenuItem item, Palette palette)
    {
        item.ForeColor = palette.Text;
        item.Padding = new Padding(2, 7, 2, 7);
        item.Margin = new Padding(0, 1, 0, 1);
    }


    private void StartInterval(TimeSpan duration)
    {
        RunAction(
            () => controller.StartForInterval(settings, DateTimeOffset.UtcNow.Add(duration)),
            () => ShowNotification(text.OpenedTitle, string.Format(text.OpenedInterval, FormatDuration(duration))));
    }

    private void ToggleIndefiniteAwake()
    {
        if (controller.State is AwakeState.Off)
        {
            RunAction(
                () => controller.StartIndefinitely(settings),
                () => ShowNotification(text.OpenedTitle, text.OpenedIndefinite));
            return;
        }

        if (controller.State is AwakeState.Error error)
        {
            ShowNotification(text.ErrorTitle, error.Message, ToolTipIcon.Error);
            return;
        }

        RunAction(controller.Stop, () => ShowNotification(text.ClosedTitle, text.ClosedMessage));
    }

    private void StartCustomInterval()
    {
        var duration = IntervalDialog.ShowCustom(null, text);
        if (duration is not null)
        {
            StartInterval(duration.Value);
        }
    }

    private void CheckInterval()
    {
        try
        {
            if (controller.DetectExternalSchemeChange())
            {
                controller.RecoverExternalScheme();
                UpdateIcon();
                ShowNotification(text.ErrorTitle, text.ExternalPlanChanged, ToolTipIcon.Error);
                return;
            }

            if (controller.CheckInterval(DateTimeOffset.UtcNow))
            {
                UpdateIcon();
                ShowNotification(text.ClosedTitle, text.ExpiredMessage);
            }
        }
        catch (Exception exception)
        {
            ShowNotification(text.ErrorTitle, exception.Message, ToolTipIcon.Error);
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Resume or PowerModes.StatusChange)
        {
            CheckInterval();
        }
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        try
        {
            controller.StopAndApplyNormalSettings(settings);
            logger.Info($"系统会话结束（{e.Reason}），已尝试恢复电源策略。");
        }
        catch (Exception exception)
        {
            logger.Error("系统会话结束时恢复电源策略失败。", exception);
        }
    }

    private void RunAction(Action action, Action? onSuccess = null)
    {
        try
        {
            action();
            EnsureNativeDimDisabled();
            UpdateIcon();
            onSuccess?.Invoke();
        }
        catch (Exception exception)
        {
            ShowNotification(text.ErrorTitle, exception.Message, ToolTipIcon.Error);
        }
    }

    private void UpdateIcon()
    {
        notifyIcon.Icon = controller.State switch
        {
            AwakeState.Indefinite => indefiniteIcon,
            AwakeState.Interval => intervalIcon,
            AwakeState.Error => errorIcon,
            _ => offIcon
        };
        notifyIcon.Text = controller.State switch
        {
            AwakeState.Indefinite => text.OpenedIndefinite,
            AwakeState.Interval interval => string.Format(text.OpenedInterval, interval.EndTimeUtc.LocalDateTime.ToString("t")),
            AwakeState.Error => text.ErrorTitle,
            _ => text.AwakeOff
        };
    }

    private void UpdateMenu(ToolStripMenuItem off, ToolStripMenuItem indefinite, ToolStripMenuItem interval, ToolStripMenuItem keepScreenOn)
    {
        off.Checked = controller.State is AwakeState.Off;
        indefinite.Checked = controller.State is AwakeState.Indefinite;
        interval.Checked = controller.State is AwakeState.Interval;
        if (statusLabel is not null)
        {
            statusLabel.Text = controller.State switch
            {
                AwakeState.Indefinite => text.OpenedIndefinite,
                AwakeState.Interval running => string.Format(extraText.UntilTime, running.EndTimeUtc.LocalDateTime.ToString("t")),
                AwakeState.Error => text.ErrorTitle,
                _ => text.ClosedTitle
            };
        }

        UpdateIcon();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(settings, text, smoothDimmer.Capabilities, updated =>
        {
            var softwareChanged = updated.AllowSoftwareDimming != settings.AllowSoftwareDimming;
            settingsService.Save(updated);
            controller.ApplyNormalSettings(updated);
            settings = updated;
            if (softwareChanged)
            {
                smoothDimmer.Refresh();
            }

            EnsureNativeDimDisabled();
            RefreshLocalizedUi();
        });
        form.ShowDialog();
        if (form.UpdateLaunched)
        {
            ExitThread();
        }
    }

    private void BeginInvokeOpenSettings()
    {
        uiContext.Post(_ => OpenSettings(), null);
    }

    private void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        notifyIcon.ShowBalloonTip(5000, title, message, icon);
    }

    private string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{duration.TotalHours:0.#} h"
        : $"{duration.TotalMinutes:0} {text.Minutes}";

    private void RefreshLocalizedUi()
    {
        text = AppText.For(settings.LanguageCode);
        extraText = SettingsText.For(settings.LanguageCode);
        var previousMenu = notifyIcon.ContextMenuStrip;
        notifyIcon.ContextMenuStrip = CreateMenu();
        previousMenu?.Dispose();
        UpdateIcon();
    }
}
