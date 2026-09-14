namespace PowerAwake.Windows.Power;

/// <summary>
/// Takes over the "dim before sleep" transition from Windows and animates the panel
/// backlight smoothly instead of the abrupt jump Windows performs when the native
/// display-dim timeout elapses (and when it restores brightness on input).
/// Windows' own auto-dim setting must be disabled by the caller (Dim = 0) so it does
/// not fight this service; the native "turn off display" timeout is left untouched.
/// </summary>
public sealed class SmoothDimmingService : IDisposable
{
    private const int PollIntervalMs = 250;
    private const int MinFadeSteps = 12;
    private const int MaxFadeSteps = 40;

    private readonly DisplayBrightnessController brightness = new();
    private readonly System.Threading.Timer pollTimer;
    private readonly object syncRoot = new();

    private Func<uint> getDimSeconds = () => 0;
    private Func<byte> getDimBrightness = () => 50;
    private Func<uint> getFadeDurationMs = () => 1500;

    private CancellationTokenSource? fadeCts;
    private byte? normalBrightnessSnapshot;
    private bool isDimmed;

    public SmoothDimmingService()
    {
        IsSupported = brightness.IsAvailable;
        pollTimer = new System.Threading.Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>False when the display has no WMI-controllable backlight (e.g. external monitor only).</summary>
    public bool IsSupported { get; }

    public void Configure(Func<uint> dimSecondsProvider, Func<byte> dimBrightnessProvider, Func<uint> fadeDurationMsProvider)
    {
        lock (syncRoot)
        {
            getDimSeconds = dimSecondsProvider;
            getDimBrightness = dimBrightnessProvider;
            getFadeDurationMs = fadeDurationMsProvider;
        }
    }

    public void Start()
    {
        if (!IsSupported)
        {
            return;
        }

        pollTimer.Change(PollIntervalMs, PollIntervalMs);
    }

    public void Stop()
    {
        pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
        CancelFade();
        RestoreImmediatelyIfDimmed();
    }

    private void Poll()
    {
        uint dimSeconds;
        byte dimTarget;
        lock (syncRoot)
        {
            dimSeconds = getDimSeconds();
            dimTarget = getDimBrightness();
        }

        if (dimSeconds == 0)
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
            BeginDim(dimTarget);
        }
        else if (isDimmed && idle.TotalSeconds < dimSeconds)
        {
            BeginRestore();
        }
    }

    private void BeginDim(byte targetPercent)
    {
        var startBrightness = brightness.GetBrightness();
        if (startBrightness is null)
        {
            return;
        }

        CancelFade();
        normalBrightnessSnapshot ??= startBrightness;
        isDimmed = true;
        fadeCts = new CancellationTokenSource();
        _ = FadeAsync(startBrightness.Value, targetPercent, fadeCts.Token);
    }

    private void BeginRestore()
    {
        var target = normalBrightnessSnapshot;
        if (target is null)
        {
            isDimmed = false;
            return;
        }

        CancelFade();
        isDimmed = false;
        normalBrightnessSnapshot = null;
        var current = brightness.GetBrightness() ?? target.Value;
        fadeCts = new CancellationTokenSource();
        _ = FadeAsync(current, target.Value, fadeCts.Token);
    }

    private void RestoreImmediatelyIfDimmed()
    {
        if (isDimmed && normalBrightnessSnapshot is byte value)
        {
            brightness.SetBrightness(value);
        }

        isDimmed = false;
        normalBrightnessSnapshot = null;
    }

    private void CancelFade()
    {
        fadeCts?.Cancel();
        fadeCts?.Dispose();
        fadeCts = null;
    }

    private async Task FadeAsync(byte from, byte to, CancellationToken token)
    {
        if (from == to)
        {
            brightness.SetBrightness(to);
            return;
        }

        uint fadeDurationMs;
        lock (syncRoot)
        {
            fadeDurationMs = getFadeDurationMs();
        }

        // Fewer steps for small brightness deltas: keeps the number of WMI round-trips (the real
        // cost of a fade) proportional to how much there is to animate instead of a fixed cadence.
        var steps = Math.Clamp(Math.Abs(to - from), MinFadeSteps, MaxFadeSteps);
        var stepInterval = fadeDurationMs / (double)steps;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 1; i <= steps; i++)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            var linearT = (double)i / steps;
            var value = (byte)Math.Round(from + ((to - from) * EaseInOutQuad(linearT)));
            brightness.SetBrightness(value);

            // Schedule against the original start time so WMI call latency doesn't accumulate
            // and stretch the fade beyond the duration the user configured.
            var remaining = (stepInterval * i) - stopwatch.Elapsed.TotalMilliseconds;
            if (remaining <= 0)
            {
                continue;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(remaining), token).ConfigureAwait(false);
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
        CancelFade();
        brightness.Dispose();
    }
}
