using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The mapping snapshot rules as the version workflow's one <c>mapping_version</c> publication rule, so the freeze of
/// <see cref="LivingVersionFreezer"/> and the publish of a new live version 1 run the same rules on the stored version that a change
/// set runs on its pending result.
/// </summary>
/// <param name="loader">Reads the version as a snapshot.</param>
/// <param name="rules">Runs every mapping rule.</param>
public sealed class MappingVersionRules(MappingSetSnapshotLoader loader, SnapshotRuleRunner<MappingSetSnapshot> rules) : IPublicationRule
{
    /// <summary><c>mapping_version</c>.</summary>
    public string Kind => VersionKinds.MappingVersion;

    /// <summary>Runs every mapping rule on a stored version.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync(await loader.LoadVersionAsync(versionId, cancellationToken), cancellationToken);
    }
}
