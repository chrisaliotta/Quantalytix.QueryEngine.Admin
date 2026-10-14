using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary><c>pins_not_empty</c>: an assumption set pins at least one dimension version, because an empty set reproduces nothing.</summary>
public sealed class PinsNotEmptyRule(ITableGateway<AssumptionSetDimensionRecord> pins) : IPublicationRule
{
    /// <summary>The rule applies to assumption sets.</summary>
    public string Kind { get; } = VersionKinds.AssumptionSet;

    /// <summary>Reports an assumption set without pins.</summary>
    /// <param name="versionId">The assumption set being published.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation for a set without pins; none otherwise.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await pins.ListAsync(new { AssumptionSetId = versionId }, cancellationToken);
        if (rows.Count > 0) return [];
        return [new RuleViolation { Rule = "pins_not_empty", Message = "The assumption set pins no dimension versions." }];
    }
}
