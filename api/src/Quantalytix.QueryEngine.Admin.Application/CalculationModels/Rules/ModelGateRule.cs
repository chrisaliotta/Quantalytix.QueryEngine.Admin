using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The model gate (OI-42): a change set is refused, writing nothing, when it introduces a violation of <see cref="ModelChecks"/>.
/// A violation the stored model already has does not refuse an edit, so a model that breaks a rule can still be repaired step by
/// step. Preview and commit run it alike; a commit compares with the model as stored when it commits.
/// </summary>
public sealed class ModelGateRule : ISnapshotRule<ModelSnapshot>
{
    /// <summary>A gate: an introduced violation refuses the change set.</summary>
    public RuleStage Stage => RuleStage.Gate;

    /// <summary>The violations the change set introduces.</summary>
    /// <param name="snapshot">The model as the change set leaves it.</param>
    /// <param name="cancellationToken">Unused; the snapshot holds everything.</param>
    /// <returns>The violations the stored model does not have.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(ModelSnapshot snapshot, CancellationToken cancellationToken)
    {
        IReadOnlyList<RuleViolation> introduced = [.. ModelChecks.Violations(snapshot).Where(violation => !snapshot.Surroundings.Baseline.Contains(violation))];
        return Task.FromResult(introduced);
    }
}
