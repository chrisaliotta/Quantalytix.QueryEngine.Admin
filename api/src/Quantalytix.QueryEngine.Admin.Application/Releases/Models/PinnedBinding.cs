using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A dimension binding of a dataset version that a release pins, with the key of the pinning dataset for rule messages.</summary>
public sealed record PinnedBinding
{
    /// <summary>The <c>dataset_key</c> of the manifest row that pins the binding's dataset version.</summary>
    public required string DatasetKey { get; init; }

    /// <summary>The binding.</summary>
    public required DimensionBindingRecord Binding { get; init; }
}
