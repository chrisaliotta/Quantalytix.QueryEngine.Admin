using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>endpoint_versions_published</c>: the bridge's source and target dimensions each have a published version, because the
/// active assumption set pins them and the engine resolves the mappings' member keys in them.
/// </summary>
public sealed class EndpointVersionsPublishedRule(BridgeMappingSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "endpoint_versions_published";

    /// <summary>The kind the rule checks: bridge versions.</summary>
    public string Kind { get; } = VersionKinds.BridgeVersion;

    /// <summary>Reports each endpoint dimension that has no published version.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Up to two violations, source first.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        return
        [
            .. new[] { ("source", set.Source), ("target", set.Target) }
                .Where(endpoint => endpoint.Item2 is null)
                .Select(endpoint => new RuleViolation
                {
                    Code = Code,
                    Message = $"The bridge's {endpoint.Item1} dimension has no published version for the mappings to resolve in; publish one first."
                })
        ];
    }
}
