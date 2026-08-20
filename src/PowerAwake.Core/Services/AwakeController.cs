using PowerAwake.Core.Models;

namespace PowerAwake.Core.Services;

public sealed class AwakeController
{
    private readonly IPowerPolicy powerPolicy;
    private readonly JsonFileStore<PowerSnapshot> recoveryStore;
    private readonly IAppLogger logger;
    private PowerSnapshot? activeSnapshot;

    public AwakeController(IPowerPolicy powerPolicy, JsonFileStore<PowerSnapshot> recoveryStore, IAppLogger? logger = null)
    {
        this.powerPolicy = powerPolicy;
        this.recoveryStore = recoveryStore;
        this.logger = logger ?? NullAppLogger.Instance;
        State = new AwakeState.Off();
    }

    public AwakeState State { get; private set; }

    public bool DetectExternalSchemeChange()
    {
        if (activeSnapshot is null || State is AwakeState.Off or AwakeState.Error)
        {
            return false;
        }

        if (powerPolicy.GetActiveScheme() == activeSnapshot.SchemeGuid)
        {
            return false;
        }

        State = new AwakeState.Error(true, "检测到电源计划已切换，Awake 已安全关闭。恢复文件仍然保留。");
        logger.Error("检测到活动电源计划切换，停止 Awake 写入。", null);
        return true;
    }

    public void StartIndefinitely(AppSettings settings)
    {
        Start(settings, null);
    }

    public void ApplyNormalSettings(AppSettings settings)
    {
        var errors = SettingsValidation.Validate(settings);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(settings));
        }

        if (State is not AwakeState.Off)
        {
            return;
        }

        var schemeGuid = powerPolicy.GetActiveScheme();
        var current = powerPolicy.ReadValues(schemeGuid);
        var normal = CreateNormalValues(current, settings);
        powerPolicy.WriteValues(schemeGuid, normal);
        VerifyValues(schemeGuid, normal);
        logger.Info($"已应用正常电源策略，电源计划 {schemeGuid}。");
    }

    public void StartForInterval(AppSettings settings, DateTimeOffset endTimeUtc)
    {
        if (endTimeUtc <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(endTimeUtc), "定时结束时间必须在未来。");
        }

        Start(settings, endTimeUtc);
    }

    public void Stop()
    {
        if (State is AwakeState.Off)
        {
            return;
        }

        if (activeSnapshot is null)
        {
            throw new InvalidOperationException("缺少有效恢复快照，拒绝关闭 Awake。");
        }

        Restore(activeSnapshot);
        activeSnapshot = null;
        recoveryStore.Delete();
        State = new AwakeState.Off();
        logger.Info("Awake 已关闭并恢复电源策略。");
    }

    public void SetKeepScreenOn(AppSettings settings)
    {
        if (activeSnapshot is null || State is AwakeState.Off)
        {
            return;
        }

        var schemeGuid = powerPolicy.GetActiveScheme();
        EnsureExpectedScheme(schemeGuid);
        var values = powerPolicy.ReadValues(schemeGuid);
        var updated = values with
        {
            DimAc = settings.ApplyToAc ? (settings.KeepScreenOn ? 0u : activeSnapshot.Original.DimAc) : values.DimAc,
            DimDc = settings.ApplyToDc ? (settings.KeepScreenOn ? 0u : activeSnapshot.Original.DimDc) : values.DimDc,
            DisplayOffAc = settings.ApplyToAc ? (settings.KeepScreenOn ? 0u : activeSnapshot.Original.DisplayOffAc) : values.DisplayOffAc,
            DisplayOffDc = settings.ApplyToDc ? (settings.KeepScreenOn ? 0u : activeSnapshot.Original.DisplayOffDc) : values.DisplayOffDc
        };
        powerPolicy.WriteValues(schemeGuid, updated);
        VerifyValues(schemeGuid, updated);
    }

    public bool CheckInterval(DateTimeOffset nowUtc)
    {
        if (State is AwakeState.Interval interval && nowUtc >= interval.EndTimeUtc)
        {
            Stop();
            return true;
        }

        return false;
    }

    public void RecoverOnStartup()
    {
        var snapshot = recoveryStore.Load();
        if (snapshot is null)
        {
            return;
        }

        activeSnapshot = snapshot;
        try
        {
            Restore(snapshot);
            activeSnapshot = null;
            recoveryStore.Delete();
        }
        catch (Exception exception)
        {
            State = new AwakeState.Error(true, exception.Message);
            logger.Error("启动恢复失败，保留恢复文件。", exception);
        }
    }

    public void RecoverExternalScheme()
    {
        if (activeSnapshot is null || State is not AwakeState.Error)
        {
            return;
        }

        var activeScheme = powerPolicy.GetActiveScheme();
        if (activeScheme == activeSnapshot.SchemeGuid)
        {
            Restore(activeSnapshot);
            activeSnapshot = null;
            recoveryStore.Delete();
            State = new AwakeState.Off();
            return;
        }

        powerPolicy.WriteValues(activeSnapshot.SchemeGuid, activeSnapshot.Original, activate: false);
        if (powerPolicy.ReadValues(activeSnapshot.SchemeGuid) != activeSnapshot.Original)
        {
            throw new InvalidOperationException("旧电源计划恢复校验失败，恢复文件已保留。");
        }

        activeSnapshot = null;
        recoveryStore.Delete();
        State = new AwakeState.Off();
        logger.Info("外部电源计划切换后，已恢复旧方案设置且保持新方案活动。");
    }

    private void Start(AppSettings settings, DateTimeOffset? endTimeUtc)
    {
        var errors = SettingsValidation.Validate(settings);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(settings));
        }

        var schemeGuid = powerPolicy.GetActiveScheme();
        if (activeSnapshot is not null && activeSnapshot.SchemeGuid != schemeGuid)
        {
            throw new InvalidOperationException("活动电源计划已切换，请先处理恢复状态。");
        }

        var current = powerPolicy.ReadValues(schemeGuid);
        var normal = CreateNormalValues(current, settings);

        if (activeSnapshot is null)
        {
            powerPolicy.WriteValues(schemeGuid, normal);
            VerifyValues(schemeGuid, normal);
            activeSnapshot = new PowerSnapshot { SchemeGuid = schemeGuid, Original = normal };
            recoveryStore.Save(activeSnapshot);
        }

        var awake = CreateAwakeValues(normal, settings);

        try
        {
            powerPolicy.WriteValues(schemeGuid, awake);
            VerifyValues(schemeGuid, awake);
            State = endTimeUtc is null ? new AwakeState.Indefinite() : new AwakeState.Interval(endTimeUtc.Value);
            logger.Info($"Awake 已开启，电源计划 {schemeGuid}。");
        }
        catch (Exception exception)
        {
            State = new AwakeState.Error(true, exception.Message);
            logger.Error("开启 Awake 时写入失败，开始回滚。", exception);
            try
            {
                Restore(activeSnapshot);
                activeSnapshot = null;
                recoveryStore.Delete();
                State = new AwakeState.Off();
                logger.Info("Awake 写入失败，但回滚成功，已恢复关闭状态。");
            }
            catch (Exception rollbackException)
            {
                logger.Error("Awake 写入失败后的回滚也失败，恢复文件已保留。", rollbackException);
            }

            throw;
        }
    }

    private void Restore(PowerSnapshot snapshot)
    {
        EnsureExpectedScheme(snapshot.SchemeGuid);

        powerPolicy.WriteValues(snapshot.SchemeGuid, snapshot.Original);
        VerifyValues(snapshot.SchemeGuid, snapshot.Original);
    }

    private static PowerValues CreateNormalValues(PowerValues current, AppSettings settings) => current with
    {
        SleepAc = settings.ApplyToAc ? settings.NormalSleepSeconds : current.SleepAc,
        SleepDc = settings.ApplyToDc ? settings.NormalSleepSeconds : current.SleepDc,
        HibernateAc = settings.ApplyToAc ? settings.NormalHibernateSeconds : current.HibernateAc,
        HibernateDc = settings.ApplyToDc ? settings.NormalHibernateSeconds : current.HibernateDc,
        DimAc = settings.ApplyToAc ? settings.DisplayDimSeconds : current.DimAc,
        DimDc = settings.ApplyToDc ? settings.DisplayDimSeconds : current.DimDc,
        DisplayOffAc = settings.ApplyToAc ? settings.DisplayOffSeconds : current.DisplayOffAc,
        DisplayOffDc = settings.ApplyToDc ? settings.DisplayOffSeconds : current.DisplayOffDc,
        DimBrightnessAc = settings.ApplyToAc ? settings.DimBrightnessPercent : current.DimBrightnessAc,
        DimBrightnessDc = settings.ApplyToDc ? settings.DimBrightnessPercent : current.DimBrightnessDc
    };

    private static PowerValues CreateAwakeValues(PowerValues normal, AppSettings settings) => normal with
    {
        SleepAc = settings.ApplyToAc ? 0u : normal.SleepAc,
        SleepDc = settings.ApplyToDc ? 0u : normal.SleepDc,
        HibernateAc = settings.ApplyToAc ? 0u : normal.HibernateAc,
        HibernateDc = settings.ApplyToDc ? 0u : normal.HibernateDc,
        DimAc = settings.ApplyToAc && settings.KeepScreenOn ? 0u : normal.DimAc,
        DimDc = settings.ApplyToDc && settings.KeepScreenOn ? 0u : normal.DimDc,
        DisplayOffAc = settings.ApplyToAc && settings.KeepScreenOn ? 0u : normal.DisplayOffAc,
        DisplayOffDc = settings.ApplyToDc && settings.KeepScreenOn ? 0u : normal.DisplayOffDc
    };

    private void EnsureExpectedScheme(Guid expectedScheme)
    {
        if (powerPolicy.GetActiveScheme() != expectedScheme)
        {
            throw new InvalidOperationException("活动电源计划已切换，未写入其他电源计划。");
        }
    }

    private void VerifyValues(Guid schemeGuid, PowerValues expected)
    {
        if (powerPolicy.ReadValues(schemeGuid) != expected)
        {
            throw new InvalidOperationException("Windows 未确认电源设置已生效，Awake 未成功开启。");
        }
    }

    private sealed class NullAppLogger : IAppLogger
    {
        public static readonly NullAppLogger Instance = new();
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}