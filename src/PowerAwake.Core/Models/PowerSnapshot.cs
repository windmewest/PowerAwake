namespace PowerAwake.Core.Models;

public sealed record PowerSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public required Guid SchemeGuid { get; init; }
    public required PowerValues Original { get; init; }
}

public sealed record PowerValues
{
    public required uint SleepAc { get; init; }
    public required uint SleepDc { get; init; }
    public required uint HibernateAc { get; init; }
    public required uint HibernateDc { get; init; }
    public required uint DimAc { get; init; }
    public required uint DimDc { get; init; }
    public required uint DisplayOffAc { get; init; }
    public required uint DisplayOffDc { get; init; }
    public required uint DimBrightnessAc { get; init; }
    public required uint DimBrightnessDc { get; init; }
}