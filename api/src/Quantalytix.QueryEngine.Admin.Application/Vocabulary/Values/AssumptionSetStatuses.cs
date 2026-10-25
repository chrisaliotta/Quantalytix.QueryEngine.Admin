namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>assumption_set_status</c> vocabulary: whether a set holds the default versions or is a captured snapshot.</summary>
public static class AssumptionSetStatuses
{
    /// <summary>The one set that pins every published version; every publish repoints it.</summary>
    public const string Active = "active";

    /// <summary>A snapshot of the active set, captured under its own key; its pins never change.</summary>
    public const string Historical = "historical";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "assumption_set_status",
        Description = "Whether an assumption set holds the default versions or is a captured snapshot.",
        Values =
        [
            new() { Value = Active, Description = "The one set that pins every published version; every publish repoints it." },
            new() { Value = Historical, Description = "A snapshot of the active set, captured under its own key; its pins never change." }
        ]
    };
}
