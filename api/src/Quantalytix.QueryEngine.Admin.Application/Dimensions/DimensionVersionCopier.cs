using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Copies a dimension version into the dimension's next draft, for the freeze.</summary>
/// <param name="versions">Creates the draft as a clone, copying the source's range data type.</param>
public sealed class DimensionVersionCopier(DimensionVersionService versions) : ILivingVersionCopier
{
    /// <summary><c>dimension_version</c>.</summary>
    public string Kind => VersionKinds.DimensionVersion;

    /// <summary>Creates the dimension's next draft as a copy of a version.</summary>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft's id.</returns>
    public async Task<long> CopyAsync(long sourceVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(sourceVersionId, cancellationToken);
        var copy = await versions.CreateAsync(source.DimensionId, new NewDimensionVersionInput { CloneFromVersionId = sourceVersionId }, cancellationToken);
        return copy.DimensionVersionId;
    }
}
