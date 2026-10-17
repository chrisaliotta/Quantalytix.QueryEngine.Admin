namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>One line of a report version's complete line tree.</summary>
public sealed record ReportLineInput
{
    /// <summary>The line's ID when it already exists; null for a new line.</summary>
    public long? ReportLineId { get; init; }

    /// <summary>Key, unique among the version's lines.</summary>
    public required string LineKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>report_line_kind</c> value.</summary>
    public required string LineKind { get; init; }

    /// <summary>The key of the enclosing line, which comes earlier in the list; null for a top-level line.</summary>
    public string? ParentLineKey { get; init; }

    /// <summary>The report-item member a <c>member</c> line shows.</summary>
    public string? ReportMemberKey { get; init; }

    /// <summary>The calculated measure key of the report's model version that a <c>formula</c> line evaluates.</summary>
    public string? CalculatedMeasureKey { get; init; }

    /// <summary>The key of the column a <c>formula</c> line places its value in.</summary>
    public string? ValueColumnKey { get; init; }

    /// <summary>Display default passed through to clients.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Optional presentation settings passed through to clients.</summary>
    public string? SettingsJson { get; init; }
}
