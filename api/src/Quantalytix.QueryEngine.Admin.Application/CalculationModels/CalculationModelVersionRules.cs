using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The model checks as the calculation model kind's <see cref="IVersionRules"/>, which restore runs on a stored version: every model
/// rule and every report rule of the reports over it in the version's snapshot.
/// </summary>
/// <param name="loader">Reads the version with the reports over it.</param>
public sealed class CalculationModelVersionRules(ModelSnapshotLoader loader) : IVersionRules
{
    /// <summary><c>calculation_model</c>.</summary>
    public string Kind => SnapshotKinds.CalculationModel;

    /// <summary>Runs every model check on a stored version.</summary>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return ModelChecks.Violations(await loader.LoadVersionAsync(versionId, cancellationToken));
    }
}
