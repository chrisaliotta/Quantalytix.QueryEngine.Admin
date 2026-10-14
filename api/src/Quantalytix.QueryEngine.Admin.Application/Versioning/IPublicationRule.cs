namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// A semantic check a version must pass before it leaves draft (draft to validated; draft to published for assumption
/// sets). Feature slices register their rules as <see cref="IPublicationRule"/>; the workflow runs every rule of the kind
/// and rejects the transition when any rule reports a violation.
/// </summary>
public interface IPublicationRule
{
    /// <summary>The <c>version_kind</c> value the rule checks.</summary>
    string Kind { get; }

    /// <summary>Checks one version.</summary>
    /// <param name="versionId">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The violations found; empty when the version passes.</returns>
    Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken);
}
