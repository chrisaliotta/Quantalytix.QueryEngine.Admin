using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>dimension_version_published</c>: the pinned dimension version is published, because the engine resolves mappings only
/// for the published dimension versions it selects.
/// </summary>
public sealed class DimensionVersionPublishedRule(
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionVersionRecord> dimensionVersions)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "dimension_version_published";

    /// <summary>The kind the rule checks: mapping versions.</summary>
    public string Kind { get; } = VersionKinds.MappingVersion;

    /// <summary>Reports a pinned dimension version that is not published.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation when the pinned version is not published; otherwise none.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { MappingVersionId = versionId }, cancellationToken);
        var pinned = await dimensionVersions.GetAsync(new { version.DimensionVersionId }, cancellationToken);
        if (pinned.Status == PublicationStatuses.Published) return [];
        var message = $"The pinned dimension version v{pinned.VersionNumber} is {pinned.Status}; the engine resolves mappings only for published dimension versions.";
        return [new RuleViolation { Rule = Code, Message = message }];
    }
}
