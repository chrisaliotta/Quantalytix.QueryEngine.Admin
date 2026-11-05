namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_member_option</c> row: how one member of the report dimension shows in a report.</summary>
public sealed record ReportMemberOptionRecord
{
    /// <summary>The owning report version; first part of the primary key.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>The member of the report dimension; second part of the primary key.</summary>
    public required string MemberKey { get; init; }

    /// <summary>A label replacing the member's own; null keeps it.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the member's row shows; a hidden row's children take its place.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Whether the row shows the member's values; a row without values is a header.</summary>
    public required bool ShowsValues { get; init; }

    /// <summary>The key of the report style the row shows; null for its role's default style.</summary>
    public string? StyleKey { get; init; }

    /// <summary>A <c>group_presentation</c> value placing the group; null keeps the report's default presentation.</summary>
    public string? Presentation { get; init; }

    /// <summary>The label of a <c>header_and_total</c> group's total line; null keeps the row's label.</summary>
    public string? TotalLabel { get; init; }

    /// <summary>Whether the row's values, and those of every member row under it, show negated; display only.</summary>
    public required bool ReverseSign { get; init; }
}
