namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeOwner</c>: the report source a report bridge belongs to (B9, OI-31).</summary>
public sealed record BridgeOwner
{
    /// <summary>The report the source belongs to.</summary>
    public required long ReportId { get; init; }

    /// <summary>The report key.</summary>
    public required string ReportKey { get; init; }

    /// <summary>The key of the source whose rows the bridge maps.</summary>
    public required string SourceKey { get; init; }
}
