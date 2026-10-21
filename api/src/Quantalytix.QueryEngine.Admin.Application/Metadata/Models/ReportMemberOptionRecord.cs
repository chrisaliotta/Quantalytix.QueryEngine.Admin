namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_member_option</c> row: how one member of the report dimension shows in a report.</summary>
public sealed record ReportMemberOptionRecord
{
    /// <summary>The owning report; first part of the primary key.</summary>
    public required long ReportId { get; init; }

    /// <summary>The member of the report dimension; second part of the primary key.</summary>
    public required string MemberKey { get; init; }

    /// <summary>A label replacing the member's own; null keeps it.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the member's row shows; a hidden row's children take its place.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Whether the row shows the member's values; a row without values is a header.</summary>
    public required bool ShowsValues { get; init; }
}
