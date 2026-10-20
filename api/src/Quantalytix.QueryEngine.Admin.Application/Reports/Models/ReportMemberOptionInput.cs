namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>How one member of the report's dimension shows.</summary>
public sealed record ReportMemberOptionInput
{
    /// <summary>The member of the report's dimension.</summary>
    public required string MemberKey { get; init; }

    /// <summary>A label replacing the member's own; null keeps it.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the member's row shows; a hidden row's children take its place.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Whether the row shows the member's values; a row without values is a header.</summary>
    public required bool ShowsValues { get; init; }
}
