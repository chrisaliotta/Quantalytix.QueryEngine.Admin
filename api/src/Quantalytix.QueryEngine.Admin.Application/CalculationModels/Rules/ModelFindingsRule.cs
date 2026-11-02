using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Lists every violation of <see cref="ModelChecks"/> the pending model has as the preview's findings, the ones the stored model already
/// had included, which <see cref="ModelGateRule"/> lets stand in a preview; a commit is refused while any remains.
/// </summary>
public sealed class ModelFindingsRule : ISnapshotRule<ModelSnapshot>
{
    /// <summary>Commit: findings refuse the save.</summary>
    public RuleStage Stage => RuleStage.Commit;

    /// <summary>Every violation of the pending model.</summary>
    /// <param name="snapshot">The model as the change set leaves it.</param>
    /// <param name="cancellationToken">Unused; the snapshot holds everything.</param>
    /// <returns>The violations.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(ModelSnapshot snapshot, CancellationToken cancellationToken)
    {
        return Task.FromResult(ModelChecks.Violations(snapshot));
    }
}
