using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>endpoint_versions_match_bridge</c>: the source and target dimension versions are versions of the bridge's source and
/// target dimensions (schema section 18), because a bridge binding remaps members of the source binding's dimension.
/// </summary>
public sealed class EndpointVersionsMatchBridgeRule(
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionRecord> dimensions)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "endpoint_versions_match_bridge";

    /// <summary>The kind the rule checks: bridge versions.</summary>
    public string Kind { get; } = VersionKinds.BridgeVersion;

    /// <summary>Reports each endpoint version that belongs to another dimension than the bridge's endpoint.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Up to two violations, source first.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { BridgeVersionId = versionId }, cancellationToken);
        var bridge = await bridges.GetAsync(new { version.BridgeId }, cancellationToken);
        var endpoints = (await dimensionVersions.ListAsync(
                new { DimensionVersionId = new[] { version.SourceDimensionVersionId, version.TargetDimensionVersionId } },
                cancellationToken))
            .ToDictionary(endpoint => endpoint.DimensionVersionId);
        var mismatches = new List<(string End, DimensionVersionRecord Pinned, long Expected)>();
        var source = endpoints[version.SourceDimensionVersionId];
        if (source.DimensionId != bridge.SourceDimensionId) mismatches.Add(("source", source, bridge.SourceDimensionId));
        var target = endpoints[version.TargetDimensionVersionId];
        if (target.DimensionId != bridge.TargetDimensionId) mismatches.Add(("target", target, bridge.TargetDimensionId));
        if (mismatches.Count == 0) return [];

        long[] dimensionIds = [.. mismatches.SelectMany(mismatch => new[] { mismatch.Pinned.DimensionId, mismatch.Expected }).Distinct()];
        var keys = (await dimensions.ListAsync(new { DimensionId = dimensionIds }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId, dimension => dimension.DimensionKey);
        return
        [
            .. mismatches.Select(mismatch => new RuleViolation
            {
                Rule = Code,
                Message = $"The {mismatch.End} dimension version v{mismatch.Pinned.VersionNumber} belongs to dimension "
                    + $"'{keys[mismatch.Pinned.DimensionId]}', not to the bridge's {mismatch.End} dimension '{keys[mismatch.Expected]}'."
            })
        ];
    }
}
