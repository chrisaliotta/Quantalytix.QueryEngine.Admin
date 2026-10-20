using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>An assumption set with its pins.</summary>
public sealed record AssumptionSetDetail
{
    /// <summary>The set.</summary>
    public required AssumptionSetRecord Set { get; init; }

    /// <summary>Its pins, by kind and then identity key.</summary>
    public required IReadOnlyList<AssumptionPin> Pins { get; init; }
}
