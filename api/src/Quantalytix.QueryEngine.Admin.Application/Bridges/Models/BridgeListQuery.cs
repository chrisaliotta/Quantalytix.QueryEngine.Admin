namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The query of <c>GET bridges</c>: every bridge, those at one dimension, or one report source's bridge.</summary>
public sealed record BridgeListQuery
{
    /// <summary>Only the bridges whose source or target is this dimension.</summary>
    public long? DimensionId { get; init; }

    /// <summary>With <see cref="SourceKey"/>, only the report bridge of this report's source.</summary>
    public long? ReportId { get; init; }

    /// <summary>With <see cref="ReportId"/>, the report source whose bridge is wanted.</summary>
    public string? SourceKey { get; init; }
}
