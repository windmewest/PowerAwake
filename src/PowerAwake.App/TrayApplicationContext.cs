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
    private readonly Icon offIcon;
    private readonly Icon indefiniteIcon;
    private readonly Icon intervalIcon;
    private readonly Icon errorIcon;
    private readonly SingleInstanceNotification instanceNotification;
    private readonly SynchronizationContext uiContext;
    private readonly SmoothDimmingService smoothDimmer = new();
    private readonly WindowsPowerPolicy powerPolicy = new();
    private AppText text = AppText.For("system");

    public TrayApplicationContext()
    {
        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerAwake");
        settingsService = new SettingsService(Path.Combine(dataDirectory, "settings.json"));
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        settings = settingsService.Load();
        text = AppText.For(settings.LanguageCode);
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

        offIcon = StateIconFactory.Create(TrayIconState.Off);
        indefiniteIcon = StateIconFactory.Create(TrayIconState.Indefinite);
        intervalIcon = StateIconFactory.Create(TrayIconState.Interval);
        errorIcon = StateIconFactory.Create(TrayIconState.Error);

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

        smoothDimmer.Configure(GetEffectiveDimSeconds, () => settings.DimBrightnessPercent, () => settings.SmoothDimmingDurationMs);
        smoothDimmer.Start();
        EnsureNativeDimDisabled();
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
            try
            {
                controller.Stop();
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
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip CreateMenu()
    {
        var menu = new ContextMenuStrip();
        var off = new ToolStripMenuItem(text.AwakeOff) { Checked = true };
        var indefinite = new ToolStripMenuItem(text.KeepAwakeIndefinitely);
        var interval = new ToolStripMenuItem(text.KeepAwakeInterval);
        var keepScreenOn = new ToolStripMenuItem(text.KeepScreenOn) { CheckOnClick = true, Checked = settings.KeepScreenOn };
        var exit = new ToolStripMenuItem(text.Exit);
        var startWithWindows = new ToolStripMenuItem(text.StartWithWindows) { CheckOnClick = true, Checked = settings.StartWithWindows };

        off.Click += (_, _) => RunAction(controller.Stop, () => ShowNotification(text.ClosedTitle, text.ClosedMessage));
        indefinite.Click += (_, _) => RunAction(() => controller.StartIndefinitely(settings), () => ShowNotification(text.OpenedTitle, text.OpenedIndefinite));
        interval.DropDownItems.Add($"15 {text.Minutes}", null, (_, _) => StartInterval(TimeSpan.FromMinutes(15)));
        interval.DropDownItems.Add($"30 {text.Minutes}", null, (_, _) => StartInterval(TimeSpan.FromMinutes(30)));
        interval.DropDownItems.Add("1 小时", null, (_, _) => StartInterval(TimeSpan.FromHours(1)));
        interval.DropDownItems.Add("2 小时", null, (_, _) => StartInterval(TimeSpan.FromHours(2)));
        interval.DropDownItems.Add(text.Custom, null, (_, _) => StartCustomInterval());
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
        exit.Click += (_, _) => ExitThread();

        menu.Items.Add(new ToolStripLabel("powerAwake"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(off);
        menu.Items.Add(indefinite);
        menu.Items.Add(interval);
        menu.Items.Add(keepScreenOn);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(text.Settings, null, (_, _) => OpenSettings());
        menu.Items.Add(startWithWindows);
        menu.Items.Add(exit);
        menu.Opening += (_, _) => UpdateMenu(menu, off, indefinite, keepScreenOn);
        return menu;
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
            controller.Stop();
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

    private void UpdateMenu(ContextMenuStrip menu, ToolStripMenuItem off, ToolStripMenuItem indefinite, ToolStripMenuItem keepScreenOn)
    {
        off.Checked = controller.State is AwakeState.Off;
        indefinite.Checked = controller.State is AwakeState.Indefinite;
        UpdateIcon();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(settings, text, updated =>
        {
            settingsService.Save(updated);
            controller.ApplyNormalSettings(updated);
            settings = updated;
            EnsureNativeDimDisabled();
            RefreshLocalizedUi();
        });
        form.ShowDialog();
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
        var previousMenu = notifyIcon.ContextMenuStrip;
        notifyIcon.ContextMenuStrip = CreateMenu();
        previousMenu?.Dispose();
        UpdateIcon();
    }
}