using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Copies a bridge version, its cardinality and allocation mode included, into the bridge's next draft, for the freeze.</summary>
/// <param name="versions">Creates the draft as a clone.</param>
public sealed class BridgeVersionCopier(BridgeVersionService versions) : ILivingVersionCopier
{
    /// <summary><c>bridge_version</c>.</summary>
    public string Kind => VersionKinds.BridgeVersion;

    /// <summary>Creates the bridge's next draft as a copy of a version.</summary>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft's id.</returns>
    public async Task<long> CopyAsync(long sourceVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(sourceVersionId, cancellationToken);
        var input = new NewBridgeVersionInput
        {
            Cardinality = source.Cardinality,
            AllocationMode = source.AllocationMode,
            CloneFromVersionId = sourceVersionId
        };
        return (await versions.CreateAsync(source.BridgeId, input, cancellationToken)).BridgeVersionId;
    }
}
