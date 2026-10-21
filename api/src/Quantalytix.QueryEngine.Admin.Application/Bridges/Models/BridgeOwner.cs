namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The report source that owns a report bridge.</summary>
public sealed record BridgeOwner
{
    /// <summary>The report the source belongs to.</summary>
    public required long ReportId { get; init; }

    /// <summary>The <c>report_key</c> of the report.</summary>
    public required string ReportKey { get; init; }

    /// <summary>The source whose rows the bridge maps.</summary>
    public required long ReportSourceId { get; init; }

    /// <summary>The <c>source_key</c> of the source.</summary>
    public required string SourceKey { get; init; }
}
