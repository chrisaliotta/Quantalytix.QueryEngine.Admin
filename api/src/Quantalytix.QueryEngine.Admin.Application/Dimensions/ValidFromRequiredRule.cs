using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>valid_from_required</c>: a version needs the start of its validity before it can be published, because the engine
/// resolves unpinned bindings to the published version whose window contains the request time.
/// </summary>
public sealed class ValidFromRequiredRule(ITableGateway<DimensionVersionRecord> versions) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "valid_from_required";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports a version without a <c>valid_from</c>.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation when the start is missing; none otherwise.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        if (version.ValidFrom is not null) return [];
        return [new RuleViolation { Rule = Code, Message = $"Version v{version.VersionNumber} has no valid_from; a published version needs the start of its validity." }];
    }
}
