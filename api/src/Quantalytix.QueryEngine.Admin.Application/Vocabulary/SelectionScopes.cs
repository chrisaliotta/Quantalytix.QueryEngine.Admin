namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>selection_scope</c> vocabulary: which report-item node a variable reads on each report line.</summary>
public static class SelectionScopes
{
    /// <summary>The line's own node.</summary>
    public const string AllMapped = "all_mapped";

    /// <summary>The variable's target member, which must be a leaf.</summary>
    public const string Exact = "exact";

    /// <summary>Whichever of the line's node and the target member lies inside the other's subtree.</summary>
    public const string Descendants = "descendants";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "selection_scope",
        Description = "Which report-item node a variable reads on each report line.",
        Values =
        [
            new() { Value = AllMapped, Description = "The line's own node." },
            new() { Value = Exact, Description = "The variable's target member, which must be a leaf." },
            new() { Value = Descendants, Description = "Whichever of the line's node and the target member lies inside the other's subtree." }
        ]
    };
}
