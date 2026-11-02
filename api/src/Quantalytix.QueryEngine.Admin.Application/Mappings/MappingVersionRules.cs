using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The mapping snapshot rules as the mapping set kind's <see cref="IVersionRules"/>, which restore runs on a stored version.</summary>
/// <param name="loader">Reads the version as a snapshot.</param>
/// <param name="rules">Runs every mapping rule.</param>
public sealed class MappingVersionRules(MappingSetSnapshotLoader loader, SnapshotRuleRunner<MappingSetSnapshot> rules) : IVersionRules
{
    /// <summary><c>mapping_set</c>.</summary>
    public string Kind => SnapshotKinds.MappingSet;

    /// <summary>Runs every mapping rule on a stored version.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync(await loader.LoadVersionAsync(versionId, cancellationToken), cancellationToken);
    }
}
