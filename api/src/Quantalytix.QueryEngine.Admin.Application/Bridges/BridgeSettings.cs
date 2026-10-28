using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The settings a bridge version may take (B5): <c>many_to_one</c> allows <c>none</c> only; <c>many_to_many</c> allows <c>partition</c> or <c>replicate</c>.</summary>
public static class BridgeSettings
{
    /// <summary>Whether a cardinality allows an allocation mode.</summary>
    /// <param name="cardinality">A <c>cardinality</c> value.</param>
    /// <param name="allocationMode">An <c>allocation_mode</c> value.</param>
    /// <returns>True when the pair is allowed.</returns>
    public static bool Allows(string cardinality, string allocationMode)
    {
        return cardinality == Cardinalities.ManyToOne
            ? allocationMode == AllocationModes.None
            : allocationMode is AllocationModes.Partition or AllocationModes.Replicate;
    }
}
