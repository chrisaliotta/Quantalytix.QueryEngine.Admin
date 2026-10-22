using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>dimension_consumers_resolve</c>: every level and member key that live objects name in the dimension exists in the version,
/// because publishing it makes it the version they resolve their keys in. <see cref="DimensionConsumers"/> lists the objects.
/// </summary>
public sealed class DimensionConsumersResolveRule(
    ITableGateway<DimensionVersionRecord> versions,
    DimensionConsumers consumers,
    PublishedDimensionReader reader)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "dimension_consumers_resolve";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports every key a live object names that the version lacks.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per missing key reference.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var keys = await reader.KeysAsync(version, cancellationToken);
        return
        [
            .. (await consumers.ListAsync(version.DimensionId, cancellationToken))
                .Where(reference => !keys.Has(reference))
                .Select(reference => new RuleViolation
                {
                    Rule = Code,
                    Message = $"{reference.Consumer} names {(reference.IsLevel ? "level" : "member")} '{reference.Key}', which v{version.VersionNumber} lacks."
                })
        ];
    }
}
