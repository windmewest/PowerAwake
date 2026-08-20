namespace PowerAwake.Core.Models;

public abstract record AwakeState
{
    private AwakeState()
    {
    }

    public sealed record Off : AwakeState;

    public sealed record Indefinite : AwakeState;

    public sealed record Interval(DateTimeOffset EndTimeUtc) : AwakeState;

    public sealed record Error(bool RecoveryAvailable, string Message) : AwakeState;
}