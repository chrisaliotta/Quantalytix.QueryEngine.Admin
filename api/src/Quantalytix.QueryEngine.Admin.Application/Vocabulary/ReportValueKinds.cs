namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>report_value_kind</c> vocabulary: what a report column returns.</summary>
public static class ReportValueKinds
{
    /// <summary>A model variable.</summary>
    public const string Variable = "variable";

    /// <summary>A model calculated measure.</summary>
    public const string Calculated = "calculated";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "report_value_kind",
        Description = "What a report column returns.",
        Values =
        [
            new() { Value = Variable, Description = "A model variable." },
            new() { Value = Calculated, Description = "A model calculated measure." }
        ]
    };
}
