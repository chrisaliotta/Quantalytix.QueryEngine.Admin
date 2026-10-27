using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Copies a mapping version into the mapping set's next draft, for the freeze.</summary>
/// <param name="versions">Creates the draft as a clone.</param>
public sealed class MappingVersionCopier(MappingVersionService versions) : ILivingVersionCopier
{
    /// <summary><c>mapping_version</c>.</summary>
    public string Kind => VersionKinds.MappingVersion;

    /// <summary>Creates the mapping set's next draft as a copy of a version.</summary>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft's id.</returns>
    public async Task<long> CopyAsync(long sourceVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(sourceVersionId, cancellationToken);
        var copy = await versions.CreateAsync(source.MappingSetId, new NewMappingVersionInput { CloneFromVersionId = sourceVersionId }, cancellationToken);
        return copy.MappingVersionId;
    }
}
