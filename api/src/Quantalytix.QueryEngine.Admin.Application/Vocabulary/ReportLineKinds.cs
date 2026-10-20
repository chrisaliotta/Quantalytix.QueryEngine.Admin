namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>report_line_kind</c> vocabulary: what an overlay row adds to the rows of the report dimension's members.</summary>
public static class ReportLineKinds
{
    /// <summary>A header that groups the rows beneath it and holds no values.</summary>
    public const string Section = "section";

    /// <summary>One calculated measure evaluated at report scope and placed in one column.</summary>
    public const string Formula = "formula";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "report_line_kind",
        Description = "What an overlay row adds to the rows of the report dimension's members.",
        Values =
        [
            new() { Value = Section, Description = "A header that groups the rows beneath it and holds no values." },
            new() { Value = Formula, Description = "One calculated measure evaluated at report scope and placed in one column." }
        ]
    };
}
