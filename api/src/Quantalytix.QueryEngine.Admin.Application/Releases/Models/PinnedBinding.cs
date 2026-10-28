using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A dimension binding of a dataset version that a manifest pins, with its manifest row's dataset and the binding's name.</summary>
public sealed record PinnedBinding
{
    /// <summary>The dataset of the manifest row that pins the binding's dataset version.</summary>
    public required long DatasetId { get; init; }

    /// <summary>That dataset's <c>dataset_key</c>, which rule messages name.</summary>
    public required string DatasetKey { get; init; }

    /// <summary>The binding.</summary>
    public required DimensionBindingRecord Binding { get; init; }

    /// <summary>The binding's name: its role's key, or a time binding's field key.</summary>
    public required string Name { get; init; }
}
