using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>bands_non_overlapping</c>: in ordinal order, each band starts after the previous one ends, taking inclusivity into
/// account, so no source value falls in two bands and duplicates its fact row. A null bound is unbounded, so only the
/// first band may lack a lower bound and only the last an upper one.
/// </summary>
public sealed class BandsNonOverlappingRule(RangeBandSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "bands_non_overlapping";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Compares each band with the one before it, in the version's bound family.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per band that does not start after its predecessor ends.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        if (set.RangeDataType is not { } family) return [];
        var violations = new List<RuleViolation>();
        for (var index = 1; index < set.Bands.Count; index++)
        {
            var previous = set.Bands[index - 1];
            var band = set.Bands[index];
            if (RangeBandBounds.StartsAfter(RangeBandBounds.Of(band, family), RangeBandBounds.Of(previous, family))) continue;
            violations.Add(new RuleViolation
            {
                Rule = Code,
                Message = $"{set.Describe(band)} overlaps {set.Describe(previous)}: each band must start after the previous one ends."
            });
        }

        return violations;
    }
}
