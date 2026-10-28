using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>allocation_replicate</c>: with allocation mode <c>replicate</c>, weights are 0 or more (B5). A replicated source counts fully in
/// every target, so the weights need not sum to anything.
/// </summary>
public sealed class AllocationReplicateRule : BridgeRule
{
    /// <summary><c>allocation_replicate</c>.</summary>
    protected override string Code => "allocation_replicate";

    /// <summary>Reports every negative weight; other modes have nothing to check.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>One violation per negative weight.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot)
    {
        return snapshot.Version.AllocationMode == AllocationModes.Replicate ? NegativeWeights(snapshot) : [];
    }
}
