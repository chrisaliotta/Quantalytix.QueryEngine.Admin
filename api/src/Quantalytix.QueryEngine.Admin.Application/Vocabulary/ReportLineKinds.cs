namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>report_line_kind</c> vocabulary: what a report line holds.</summary>
public static class ReportLineKinds
{
    /// <summary>A header that groups the lines beneath it and holds no values.</summary>
    public const string Section = "section";

    /// <summary>The values of one report-item member, computed over its subtree.</summary>
    public const string Member = "member";

    /// <summary>One calculated measure evaluated at report scope and placed in one column.</summary>
    public const string Formula = "formula";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "report_line_kind",
        Description = "What a report line holds.",
        Values =
        [
            new() { Value = Section, Description = "A header that groups the lines beneath it and holds no values." },
            new() { Value = Member, Description = "The values of one report-item member, computed over its subtree." },
            new() { Value = Formula, Description = "One calculated measure evaluated at report scope and placed in one column." }
        ]
    };
}
