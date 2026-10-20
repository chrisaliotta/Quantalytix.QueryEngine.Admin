namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>selection_scope</c> vocabulary: which report dimension node a variable reads on each report row.</summary>
public static class SelectionScopes
{
    /// <summary>The row's own node; a variable the report does not target reads it.</summary>
    public const string AllMapped = "all_mapped";

    /// <summary>The variable's target member, which must be a leaf.</summary>
    public const string Exact = "exact";

    /// <summary>Whichever of the row's node and the target member lies inside the other's subtree.</summary>
    public const string Descendants = "descendants";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "selection_scope",
        Description = "Which report dimension node a variable reads on each report row.",
        Values =
        [
            new() { Value = AllMapped, Description = "The row's own node; a variable the report does not target reads it." },
            new() { Value = Exact, Description = "The variable's target member, which must be a leaf." },
            new() { Value = Descendants, Description = "Whichever of the row's node and the target member lies inside the other's subtree." }
        ]
    };
}
