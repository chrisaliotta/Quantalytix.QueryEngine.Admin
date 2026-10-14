namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The editable settings of a draft assumption set; its key is fixed at creation.</summary>
public sealed record AssumptionSetUpdateInput
{
    /// <summary>The human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The optional business purpose or provenance.</summary>
    public string? Description { get; init; }

    /// <summary>The semantic time (UTC) at which capturing resolves the effective dimension versions.</summary>
    public required DateTime EffectiveAt { get; init; }
}
