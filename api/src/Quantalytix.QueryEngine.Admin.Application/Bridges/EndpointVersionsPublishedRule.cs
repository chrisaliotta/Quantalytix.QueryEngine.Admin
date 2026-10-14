using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>endpoint_versions_published</c>: both endpoint versions are published, because the engine loads the members of
/// published dimension versions only, so a bridge between drafts would remap members queries never see.
/// </summary>
public sealed class EndpointVersionsPublishedRule(
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<DimensionVersionRecord> dimensionVersions)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "endpoint_versions_published";

    /// <summary>The kind the rule checks: bridge versions.</summary>
    public string Kind { get; } = VersionKinds.BridgeVersion;

    /// <summary>Reports each endpoint version that is not published.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Up to two violations, source first.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { BridgeVersionId = versionId }, cancellationToken);
        var endpoints = (await dimensionVersions.ListAsync(
                new { DimensionVersionId = new[] { version.SourceDimensionVersionId, version.TargetDimensionVersionId } },
                cancellationToken))
            .ToDictionary(endpoint => endpoint.DimensionVersionId);
        (string End, DimensionVersionRecord Pinned)[] pinned =
        [
            ("source", endpoints[version.SourceDimensionVersionId]),
            ("target", endpoints[version.TargetDimensionVersionId])
        ];
        return
        [
            .. pinned
                .Where(endpoint => endpoint.Pinned.Status != PublicationStatuses.Published)
                .Select(endpoint => new RuleViolation
                {
                    Rule = Code,
                    Message = $"The {endpoint.End} dimension version v{endpoint.Pinned.VersionNumber} is {endpoint.Pinned.Status}; "
                        + "the engine resolves bridges only between published dimension versions."
                })
        ];
    }
}
