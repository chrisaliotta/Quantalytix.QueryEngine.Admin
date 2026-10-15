namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// A semantic check of one version that guards a status transition. <see cref="IPublicationRule"/> and
/// <see cref="IRetirementRule"/> say which transition; the workflow runs every rule of the kind before that transition and
/// rejects it when any rule reports a violation.
/// </summary>
public interface IVersionRule
{
    /// <summary>The <c>version_kind</c> value the rule checks.</summary>
    string Kind { get; }

    /// <summary>Checks one version.</summary>
    /// <param name="versionId">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The violations found; empty when the version passes.</returns>
    Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken);
}
