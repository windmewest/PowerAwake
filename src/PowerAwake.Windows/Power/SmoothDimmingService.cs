namespace PowerAwake.Windows.Power;

/// <summary>
/// Takes over the "dim before sleep" transition from Windows and animates every dimmable display
/// smoothly instead of the abrupt jump Windows performs when the native display-dim timeout
/// elapses (and when it restores brightness on input).
/// Displays are dimmed through the best method each one offers: the laptop panel (WMI), external
/// monitors with DDC/CI, or - when allowed and nothing else works, as on many desktop monitors -
/// a software overlay. Windows' own auto-dim must be disabled by the caller (Dim = 0) so it does
/// not fight this service; the native "turn off display" timeout is left untouched.
/// </summary>
public sealed class SmoothDimmingService : IDisposable
{
    private const int PollIntervalMs = 250;
    private const int FrameIntervalMs = 20;

    private readonly System.Threading.Timer pollTimer;
    private readonly object stateLock = new();

    private Func<uint> getDimSeconds = () => 0;
    private Func<byte> getDimBrightness = () => 50;
    private Func<uint> getFadeDurationMs = () => 1500;
    private Func<bool> getAllowSoftware = () => true;

    private IReadOnlyList<IBrightnessTarget> targets = Array.Empty<IBrightnessTarget>();
    private CancellationTokenSource? fadeCts;
    private Task fadeTask = Task.CompletedTask;
    private byte?[]? snapshot;
    private bool isDimmed;
    private bool started;

    public SmoothDimmingService()
    {
        pollTimer = new System.Threading.Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public DisplayCapabilities Capabilities { get; private set; } = new(0, 0, 0);

    /// <summary>False when no display can be dimmed (e.g. software dimming disabled and no hardware control).</summary>
    public bool IsSupported => Capabilities.IsAnySupported;

    public void Configure(
        Func<uint> dimSecondsProvider,
        Func<byte> dimBrightnessProvider,
        Func<uint> fadeDurationMsProvider,
        Func<bool>? allowSoftwareProvider = null)
    {
        lock (stateLock)
        {
            getDimSeconds = dimSecondsProvider;
            getDimBrightness = dimBrightnessProvider;
            getFadeDurationMs = fadeDurationMsProvider;
            getAllowSoftware = allowSoftwareProvider ?? (() => true);
        }
    }

    public void Start()
    {
        Refresh();
        lock (stateLock)
        {
            started = true;
        }

        pollTimer.Change(PollIntervalMs, PollIntervalMs);
    }

    public void Stop()
    {
        pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
        lock (stateLock)
        {
            started = false;
            CancelFadeAndWait();
            RestoreImmediatelyIfDimmed();
        }
    }

    /// <summary>Re-detects displays (hot-plug, resolution change, or a changed software-dimming setting).</summary>
    public void Refresh()
    {
        lock (stateLock)
        {
            CancelFadeAndWait();
            RestoreImmediatelyIfDimmed();
            DisposeTargets();
            targets = DisplayBrightnessController.Discover(getAllowSoftware());
            Capabilities = DisplayBrightnessController.Describe(targets);
        }
    }

    private void Poll()
    {
        if (!Monitor.TryEnter(stateLock))
        {
            return;
        }

        try
        {
            if (!started)
            {
                return;
            }

            var dimSeconds = getDimSeconds();
            if (dimSeconds == 0 || targets.Count == 0)
            {
                if (isDimmed)
                {
                    BeginRestore();
                }

                return;
            }

            var idle = IdleTimeReader.GetIdleTime();
            if (!isDimmed && idle.TotalSeconds >= dimSeconds)
            {
                BeginDim(getDimBrightness());
            }
            else if (isDimmed && idle.TotalSeconds < dimSeconds)
            {
                BeginRestore();
            }
        }
        finally
        {
            Monitor.Exit(stateLock);
        }
    }

    private void BeginDim(byte targetPercent)
    {
        var current = targets.Select(t => t.Get()).ToArray();
        if (current.All(value => value is null))
        {
            return;
        }

        CancelFadeAndWait();
        snapshot ??= current;
        isDimmed = true;
        var goals = current.Select(value => value is byte v ? (byte?)Math.Min(v, targetPercent) : null).ToArray();
        StartFade(current, goals);
    }

    private void BeginRestore()
    {
        var saved = snapshot;
        if (saved is null)
        {
            isDimmed = false;
            return;
        }

        CancelFadeAndWait();
        isDimmed = false;
        snapshot = null;
        var current = targets.Select((t, i) => t.Get() ?? saved[i]).ToArray();
        StartFade(current, saved);
    }

    private void StartFade(byte?[] from, byte?[] to)
    {
        fadeCts = new CancellationTokenSource();
        var token = fadeCts.Token;
        var activeTargets = targets;
        var duration = getFadeDurationMs();
        fadeTask = Task.Run(() => FadeAsync(activeTargets, from, to, duration, token));
    }

    private void RestoreImmediatelyIfDimmed()
    {
        if (isDimmed && snapshot is { } saved)
        {
            for (var i = 0; i < targets.Count && i < saved.Length; i++)
            {
                if (saved[i] is byte value)
                {
                    targets[i].Set(value);
                }
            }
        }

        isDimmed = false;
        snapshot = null;
    }

    private void CancelFadeAndWait()
    {
        if (fadeCts is null)
        {
            return;
        }

        fadeCts.Cancel();
        try
        {
            fadeTask.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // A cancelled fade is expected.
        }

        fadeCts.Dispose();
        fadeCts = null;
    }

    private void DisposeTargets()
    {
        foreach (var target in targets)
        {
            target.Dispose();
        }

        targets = Array.Empty<IBrightnessTarget>();
        Capabilities = new DisplayCapabilities(0, 0, 0);
    }

    private static async Task FadeAsync(IReadOnlyList<IBrightnessTarget> fadeTargets, byte?[] from, byte?[] to, uint durationMs, CancellationToken token)
    {
        var last = new int[fadeTargets.Count];
        Array.Fill(last, -1);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Progress is derived from wall-clock time rather than a fixed step count, so slow
        // backends (WMI, DDC/CI) skip intermediate values instead of stretching the fade.
        while (!token.IsCancellationRequested)
        {
            var progress = durationMs == 0 ? 1.0 : Math.Min(1.0, stopwatch.Elapsed.TotalMilliseconds / durationMs);
            var eased = EaseInOutQuad(progress);

            void Apply(int i)
            {
                if (from[i] is not byte start || to[i] is not byte end)
                {
                    return;
                }

                var value = (int)Math.Round(start + ((end - start) * eased));
                if (value == last[i])
                {
                    return;
                }

                last[i] = value;
                fadeTargets[i].Set((byte)value);
            }

            if (fadeTargets.Count > 1)
            {
                Parallel.For(0, fadeTargets.Count, Apply);
            }
            else if (fadeTargets.Count == 1)
            {
                Apply(0);
            }

            if (progress >= 1.0)
            {
                return;
            }

            try
            {
                await Task.Delay(FrameIntervalMs, token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private static double EaseInOutQuad(double t) => t < 0.5 ? 2 * t * t : 1 - (Math.Pow((-2 * t) + 2, 2) / 2);

    public void Dispose()
    {
        pollTimer.Dispose();
        lock (stateLock)
        {
            started = false;
            CancelFadeAndWait();
            RestoreImmediatelyIfDimmed();
            DisposeTargets();
        }
    }
}
