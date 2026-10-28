using Quantalytix.QueryEngine.Admin.Application.Bridges;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The rules one bridge source adds into one row, which its report bridge stores as its mappings into that row.</summary>
public sealed record ReportBridgeRow
{
    /// <summary>The bridge source.</summary>
    public required long ReportSourceId { get; init; }

    /// <summary>The data row.</summary>
    public required string RowKey { get; init; }

    /// <summary>The row's complete rule list.</summary>
    public required IReadOnlyList<ReportBridgeRule> Rules { get; init; }
}
