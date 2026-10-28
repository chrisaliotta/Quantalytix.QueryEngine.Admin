using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Copies a bridge version, its settings included, into the bridge's next draft, for the freeze.</summary>
/// <param name="versions">Reads the version's bridge and settings.</param>
/// <param name="drafts">Creates the draft as a copy.</param>
public sealed class BridgeVersionCopier(ITableGateway<BridgeVersionRecord> versions, BridgeDrafts drafts) : ILivingVersionCopier
{
    /// <summary><c>bridge_version</c>.</summary>
    public string Kind => VersionKinds.BridgeVersion;

    /// <summary>Creates the bridge's next draft as a copy of a version.</summary>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft's id.</returns>
    public async Task<long> CopyAsync(long sourceVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(new { BridgeVersionId = sourceVersionId }, cancellationToken);
        return (await drafts.CreateAsync(source.BridgeId, source.Cardinality, source.AllocationMode, sourceVersionId, cancellationToken)).BridgeVersionId;
    }
}
