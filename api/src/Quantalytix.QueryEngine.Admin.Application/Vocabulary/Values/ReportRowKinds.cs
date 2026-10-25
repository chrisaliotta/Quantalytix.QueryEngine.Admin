namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>report_row_kind</c> vocabulary: the kinds of row a report's editor lays out. Data and group rows are members of the
/// report dimension; section and formula rows are the report's overlay rows, with the <c>report_line_kind</c> values.
/// </summary>
public static class ReportRowKinds
{
    /// <summary>An assignable member row whose values come from the source items mapped to it and the rows under it.</summary>
    public const string Data = "data";

    /// <summary>A member row that takes no items and totals the rows under it, or only heads them when it shows no values.</summary>
    public const string Group = "group";

    /// <summary>An overlay header row, which no source maps to.</summary>
    public const string Section = ReportLineKinds.Section;

    /// <summary>An overlay row that places one calculated measure in one column.</summary>
    public const string Formula = ReportLineKinds.Formula;

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "report_row_kind",
        Description = "Kind of a row of a report.",
        Values =
        [
            new() { Value = Data, Description = "A member row whose values come from the source items mapped to it and the rows under it." },
            new() { Value = Group, Description = "A member row that takes no items and totals the rows under it, or only heads them when it shows no values." },
            new() { Value = Section, Description = "An overlay header row, which no source maps to." },
            new() { Value = Formula, Description = "An overlay row that places one calculated measure in one column." }
        ]
    };

    /// <summary>Whether rows of a kind are members of the report dimension rather than overlay rows.</summary>
    /// <param name="kind">A <c>report_row_kind</c> value.</param>
    /// <returns>True for data and group rows.</returns>
    public static bool IsMember(string kind)
    {
        return kind is Data or Group;
    }
}
