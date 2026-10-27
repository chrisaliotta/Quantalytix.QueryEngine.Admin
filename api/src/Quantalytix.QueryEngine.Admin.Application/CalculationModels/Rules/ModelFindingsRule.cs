using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Lists every violation of <see cref="ModelChecks"/> the pending model has as the preview's findings, so the editor shows the ones
/// the stored model already had, which <see cref="ModelGateRule"/> lets stand.
/// </summary>
public sealed class ModelFindingsRule : ISnapshotRule<ModelSnapshot>
{
    /// <summary>Publication: findings that do not block saving.</summary>
    public RuleStage Stage => RuleStage.Publication;

    /// <summary>Every violation of the pending model.</summary>
    /// <param name="snapshot">The model as the change set leaves it.</param>
    /// <param name="cancellationToken">Unused; the snapshot holds everything.</param>
    /// <returns>The violations.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(ModelSnapshot snapshot, CancellationToken cancellationToken)
    {
        return Task.FromResult(ModelChecks.Violations(snapshot));
    }
}
