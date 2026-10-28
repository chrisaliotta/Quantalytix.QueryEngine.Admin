namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>A report source's new bridge, for <see cref="ReportBridges.CreateAsync"/>: from the source's dimension into the report's dimension.</summary>
public sealed record ReportBridgeInput
{
    /// <summary>The bridge key the report derives from its key and the source's, such as <c>balance_sheet.gl</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The display name, the source's.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dimension of the role the source reads.</summary>
    public required long SourceDimensionId { get; init; }

    /// <summary>The report's dimension, whose members are the report's rows.</summary>
    public required long ReportDimensionId { get; init; }

    /// <summary>Another report bridge whose rules the new one starts with, never linked; null to start empty.</summary>
    public long? CopyFromBridgeId { get; init; }
}
