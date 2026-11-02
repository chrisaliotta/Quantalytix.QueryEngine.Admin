using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A snapshot's dataset version, indexed for resolving the keys and roles report sources name in it.</summary>
public sealed record HeldDatasetVersion
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

    /// <summary>Indexes a dataset version's rows.</summary>
    /// <param name="datasetKey">The dataset's key.</param>
    /// <param name="fields">The version's fields.</param>
    /// <param name="bindings">The version's bindings.</param>
    /// <param name="timeBindings">The time extensions of its bindings.</param>
    /// <param name="measures">The version's measures.</param>
    /// <returns>The indexed version.</returns>
    public static HeldDatasetVersion Of(
        string datasetKey,
        IEnumerable<FieldRecord> fields,
        IReadOnlyList<DimensionBindingRecord> bindings,
        IEnumerable<TimeBindingRecord> timeBindings,
        IEnumerable<MeasureRecord> measures)
    {
        var timeIds = timeBindings.Select(extension => extension.DimensionBindingId).ToHashSet();
        return new HeldDatasetVersion
        {
            DatasetKey = datasetKey,
            Fields = fields.ToDictionary(field => field.FieldKey, StringComparer.Ordinal),
            Bindings = bindings,
            TimeBindingIds = bindings.Where(binding => binding.BindingKind == BindingKinds.Time && timeIds.Contains(binding.DimensionBindingId))
                .Select(binding => binding.DimensionBindingId).ToHashSet(),
            Measures = measures.ToDictionary(measure => measure.MeasureKey, StringComparer.Ordinal)
        };
    }

    /// <summary>Whether a field has a time binding, which a source reads its periods on.</summary>
    /// <param name="fieldKey">A field key.</param>
    /// <returns>True when the version has the field and a time binding of it.</returns>
    public bool IsTimeField(string fieldKey)
    {
        return Fields.GetValueOrDefault(fieldKey) is { } field
            && Bindings.Any(binding => binding.SourceFieldId == field.FieldId && TimeBindingIds.Contains(binding.DimensionBindingId));
    }
}
