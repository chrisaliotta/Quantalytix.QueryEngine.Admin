using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>one_mapping_version_per_dimension_version</c>: no other non-retired version of the set pins the same dimension version
/// (schema section 9), because a mapped binding resolves exactly one mapping version from the set and its dimension version.
/// </summary>
public sealed class OneMappingVersionPerDimensionVersionRule(
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionVersionRecord> dimensionVersions)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "one_mapping_version_per_dimension_version";

    /// <summary>The kind the rule checks: mapping versions.</summary>
    public string Kind { get; } = VersionKinds.MappingVersion;

    /// <summary>Reports every other draft, validated or published version of the set that pins the same dimension version.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per such version.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { MappingVersionId = versionId }, cancellationToken);
        var siblings = (await versions.ListAsync(new { version.MappingSetId, version.DimensionVersionId }, cancellationToken))
            .Where(sibling => sibling.MappingVersionId != versionId && sibling.Status != PublicationStatuses.Retired)
            .OrderBy(sibling => sibling.VersionNumber)
            .ToList();
        if (siblings.Count == 0) return [];

        var pinned = await dimensionVersions.GetAsync(new { version.DimensionVersionId }, cancellationToken);
        return
        [
            .. siblings.Select(sibling => new RuleViolation
            {
                Rule = Code,
                Message = $"Mapping version v{sibling.VersionNumber} ({sibling.Status}) of this set already pins dimension version v{pinned.VersionNumber}."
            })
        ];
    }
}
