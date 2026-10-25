namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>boundary_match</c> vocabulary: how a boundary selection finds its observation.</summary>
public static class BoundaryMatches
{
    /// <summary>Require an observation exactly at the boundary.</summary>
    public const string Exact = "exact";

    /// <summary>Use the latest valid observation at or before the boundary, within the lookback.</summary>
    public const string OnOrBefore = "on_or_before";

    /// <summary>Use the latest valid observation strictly before the boundary.</summary>
    public const string StrictlyBefore = "strictly_before";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "boundary_match",
        Description = "How a boundary selection finds its observation.",
        Values =
        [
            new() { Value = Exact, Description = "Require an observation exactly at the boundary." },
            new() { Value = OnOrBefore, Description = "Use the latest valid observation at or before the boundary, within the lookback." },
            new() { Value = StrictlyBefore, Description = "Use the latest valid observation strictly before the boundary." }
        ]
    };
}
