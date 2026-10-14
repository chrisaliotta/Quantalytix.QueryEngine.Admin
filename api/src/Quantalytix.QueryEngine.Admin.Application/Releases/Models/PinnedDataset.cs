using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A manifest row of a release with the key of its dataset, which the release rules name in their messages.</summary>
public sealed record PinnedDataset
{
    /// <summary>The <c>dataset_key</c> of the row's dataset.</summary>
    public required string DatasetKey { get; init; }

    /// <summary>The manifest row.</summary>
    public required ReleaseDatasetRecord Row { get; init; }
}
