using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary><c>manifest_not_empty</c>: a release pins at least one dataset version, because an empty release exposes no datasets to queries.</summary>
public sealed class ManifestNotEmptyRule(ITableGateway<ReleaseDatasetRecord> manifest) : IPublicationRule
{
    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports a release whose manifest has no rows.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation for an empty manifest; none otherwise.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await manifest.ListAsync(new { SemanticReleaseId = versionId }, cancellationToken);
        if (rows.Count > 0) return [];
        return [new RuleViolation { Code = "manifest_not_empty", Message = "The release pins no dataset versions." }];
    }
}
