using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>band_boundary_family</c>: a range version's bands set only the bounds of its range data type's family, because the
/// engine compares source values against that family's columns alone; the other families stay null.
/// </summary>
public sealed class BandBoundaryFamilyRule(RangeBandSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "band_boundary_family";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>
    /// Reports every band that sets a bound of another family. Without a range data type the family is unknown, which
    /// <c>range_data_type_matches_kind</c> reports.
    /// </summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per band with foreign bounds.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        if (set.RangeDataType is not { } family) return [];
        return
        [
            .. set.Bands
                .Select(band => (Band: band, Foreign: RangeBandBounds.FamiliesOf(band).Where(used => used != family).ToList()))
                .Where(check => check.Foreign.Count > 0)
                .Select(check => new RuleViolation
                {
                    Rule = Code,
                    Message = $"{set.Describe(check.Band)} sets {string.Join(" and ", check.Foreign)} bounds, but the version's range_data_type is {family}."
                })
        ];
    }
}
