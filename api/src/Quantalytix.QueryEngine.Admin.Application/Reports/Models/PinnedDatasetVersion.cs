using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A dataset version a release pins, indexed for resolving the keys and roles report sources name in it.</summary>
public sealed record PinnedDatasetVersion
{
    /// <summary>The <c>dataset_key</c> of the dataset.</summary>
    public required string DatasetKey { get; init; }

    /// <summary>The version's fields, by key.</summary>
    public required IReadOnlyDictionary<string, FieldRecord> Fields { get; init; }

    /// <summary>The version's dimension bindings.</summary>
    public required IReadOnlyList<DimensionBindingRecord> Bindings { get; init; }

    /// <summary>The bindings that have a time extension.</summary>
    public required IReadOnlySet<long> TimeBindingIds { get; init; }

    /// <summary>The version's measures, by key.</summary>
    public required IReadOnlyDictionary<string, MeasureRecord> Measures { get; init; }
}
