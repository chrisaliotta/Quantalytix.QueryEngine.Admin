namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_line</c> row: one line of a report version's line tree.</summary>
public sealed record ReportLineRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportLineId { get; init; }

    /// <summary>The owning report version.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>Key, unique among the version's lines.</summary>
    public required string LineKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>report_line_kind</c> value.</summary>
    public required string LineKind { get; init; }

    /// <summary>The enclosing line; null for a top-level line.</summary>
    public long? ParentReportLineId { get; init; }

    /// <summary>The report-item member of a <c>member</c> line.</summary>
    public string? ReportMemberKey { get; init; }

    /// <summary>The calculated measure of a <c>formula</c> line.</summary>
    public long? CalculatedMeasureId { get; init; }

    /// <summary>The column a <c>formula</c> line places its value in.</summary>
    public long? ValueColumnId { get; init; }

    /// <summary>Order among the line's siblings.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Display default passed through to clients.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Reserved: line filters are not yet supported.</summary>
    public string? FilterJson { get; init; }

    /// <summary>Optional presentation settings passed through to clients.</summary>
    public string? SettingsJson { get; init; }
}
