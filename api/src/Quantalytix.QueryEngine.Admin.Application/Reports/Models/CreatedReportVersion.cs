using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A new draft report version, with the columns and lines a clone could not carry into its model version.</summary>
public sealed record CreatedReportVersion
{
    /// <summary>The new draft.</summary>
    public required ReportVersionRecord Version { get; init; }

    /// <summary>The keys of the cloned columns whose metric key the draft's model version lacks.</summary>
    public required IReadOnlyList<string> DroppedColumns { get; init; }

    /// <summary>The keys of the cloned formula lines whose calculated measure or value column the draft lacks.</summary>
    public required IReadOnlyList<string> DroppedLines { get; init; }
}
