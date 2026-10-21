namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>report_transform</c> vocabulary, the <c>transforms</c> of a report comparison: what each returned column derives from
/// comparing its base, the run or another comparison, with the comparison.
/// </summary>
public static class ReportTransforms
{
    /// <summary>The comparison's own value.</summary>
    public const string Comparison = "comparison";

    /// <summary>The base minus the comparison.</summary>
    public const string Change = "change";

    /// <summary>The change divided by the comparison's absolute value.</summary>
    public const string PercentChange = "percentChange";

    /// <summary>The base divided by the comparison.</summary>
    public const string Ratio = "ratio";

    /// <summary>The row's change divided by its parent's change.</summary>
    public const string Share = "share";

    /// <summary>The change split into the effects of the metric's drivers, with a residual.</summary>
    public const string Drivers = "drivers";

    /// <summary>A ratio's change split across child rows and members into rate, mix and interaction, with a residual.</summary>
    public const string Contribution = "contribution";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "report_transform",
        Description = "What a report comparison derives from each returned column.",
        Values =
        [
            new() { Value = Comparison, Description = "The comparison's own value." },
            new() { Value = Change, Description = "The base minus the comparison." },
            new() { Value = PercentChange, Description = "The change divided by the comparison's absolute value." },
            new() { Value = Ratio, Description = "The base divided by the comparison." },
            new() { Value = Share, Description = "The row's change divided by its parent's change." },
            new() { Value = Drivers, Description = "The change split into the effects of the metric's drivers, with a residual." },
            new() { Value = Contribution, Description = "A ratio's change split across child rows and members into rate, mix and interaction, with a residual." }
        ]
    };
}
