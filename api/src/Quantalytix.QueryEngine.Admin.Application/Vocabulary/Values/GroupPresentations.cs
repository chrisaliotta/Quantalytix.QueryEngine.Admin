namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>group_presentation</c> vocabulary, a report's <c>default_presentation</c> and a member option's <c>presentation</c>: how a
/// renderer places a group, a member row that shows values and has rows under it. The engine returns rows in hierarchy order; renderers
/// place them.
/// </summary>
public static class GroupPresentations
{
    /// <summary>The group's total, then the rows under it, indented.</summary>
    public const string TotalAbove = "total_above";

    /// <summary>The rows under the group, indented, then its total.</summary>
    public const string TotalBelow = "total_below";

    /// <summary>A header with the group's label, the rows under it indented, then a total line.</summary>
    public const string HeaderAndTotal = "header_and_total";

    /// <summary>The rows under the group at its own indent, then its total.</summary>
    public const string Step = "step";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "group_presentation",
        Key = "groupPresentation",
        Description = "How a renderer places a group, a member row that shows values and has rows under it.",
        Values =
        [
            new() { Value = TotalAbove, Label = "Total above", Description = "The group's total, then the rows under it, indented." },
            new() { Value = TotalBelow, Label = "Total below", Description = "The rows under the group, indented, then its total." },
            new() { Value = HeaderAndTotal, Label = "Header and total", Description = "A header, the rows under it indented, then a total line." },
            new() { Value = Step, Label = "Step", Description = "The rows under the group at its own indent, then its total." }
        ]
    };
}
