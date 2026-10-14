namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>A request to create a draft assumption set.</summary>
public sealed record AssumptionSetCreateInput
{
    /// <summary>The stable logical name, unique and fixed once created, such as <c>FY2026_Q3_CLOSE</c>.</summary>
    public required string AssumptionSetKey { get; init; }

    /// <summary>The human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The optional business purpose or provenance.</summary>
    public string? Description { get; init; }

    /// <summary>The semantic time (UTC) at which capturing resolves the effective dimension versions.</summary>
    public required DateTime EffectiveAt { get; init; }
}
