using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A dataset version a release pins, indexed for resolving the keys report sources and routes name in it.</summary>
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

    /// <summary>The binding of a field, or null when the field is missing or unbound.</summary>
    /// <param name="fieldKey">A field key.</param>
    /// <returns>The binding whose source field is the field.</returns>
    public DimensionBindingRecord? BindingOf(string fieldKey)
    {
        return Fields.TryGetValue(fieldKey, out var field) ? Bindings.FirstOrDefault(binding => binding.SourceFieldId == field.FieldId) : null;
    }
}
