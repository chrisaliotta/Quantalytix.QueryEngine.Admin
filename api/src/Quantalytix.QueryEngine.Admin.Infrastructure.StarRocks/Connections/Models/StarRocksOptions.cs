namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;

/// <summary>Settings for every StarRocks connection the API opens, bound from the <c>StarRocks</c> configuration section.</summary>
public sealed record StarRocksOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "StarRocks";

    /// <summary>Seconds a statement may run before it is cancelled; applied to every connection as its default command timeout.</summary>
    public int CommandTimeoutSeconds { get; init; } = 30;
}
