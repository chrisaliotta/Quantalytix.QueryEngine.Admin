using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary><c>band_bounds_ordered</c>: when a band has both bounds, the lower one is below the upper one, or the band matches nothing.</summary>
public sealed class BandBoundsOrderedRule(RangeBandSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "band_bounds_ordered";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports every band whose lower bound is not below its upper bound, in the version's bound family.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per such band.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        if (set.RangeDataType is not { } family) return [];
        return
        [
            .. set.Bands
                .Where(band => RangeBandBounds.Of(band, family) is { Lower: { } lower, Upper: { } upper } && lower.CompareTo(upper) >= 0)
                .Select(band => new RuleViolation { Rule = Code, Message = $"{set.Describe(band)} has a lower bound that is not below its upper bound." })
        ];
    }
}
