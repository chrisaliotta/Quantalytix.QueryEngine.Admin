using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary><c>manifest_not_empty</c>: a release pins at least one dataset version, because an empty release exposes no datasets to queries.</summary>
public sealed class ManifestNotEmptyRule : IReleaseRule
{
    /// <summary>Publication: a draft may be empty while it is edited.</summary>
    public RuleStage Stage => RuleStage.Publication;

    /// <summary>Reports a manifest with no rows.</summary>
    /// <param name="snapshot">The release.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>One violation for an empty manifest; none otherwise.</returns>
    public Task<IReadOnlyList<ReleaseViolation>> EvaluateAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReleaseViolation> violations = snapshot.Manifest.Count > 0
            ? []
            : [new ReleaseViolation { Code = "manifest_not_empty", Message = "The release pins no dataset versions.", DatasetId = null }];
        return Task.FromResult(violations);
    }
}
