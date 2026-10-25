using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Requires a series grain to leave out the observation-time field, the source field of any time binding of the version:
/// a series is one trajectory over time, so its identity cannot include the time it is observed at (schema section 7).
/// </summary>
public sealed class SeriesGrainExcludesTimeFieldRule(
    ITableGateway<DatasetGrainRecord> grains,
    ITableGateway<DatasetGrainFieldRecord> grainFields,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<FieldRecord> fields) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "series_grain_excludes_time_field";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each series grain component that is the source field of a time binding of the version.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per observation-time field in a series grain.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionId };
        var versionBindings = await bindings.ListAsync(versionFilter, cancellationToken);
        var extensions = await timeBindings.ListAsync(new { DimensionBindingId = versionBindings.Select(binding => binding.DimensionBindingId).ToArray() }, cancellationToken);
        var timeBindingIds = extensions.Select(extension => extension.DimensionBindingId).ToHashSet();
        var timeFieldIds = versionBindings
            .Where(binding => timeBindingIds.Contains(binding.DimensionBindingId) && binding.SourceFieldId is not null)
            .Select(binding => binding.SourceFieldId!.Value)
            .ToHashSet();
        var seriesGrains = (await grains.ListAsync(versionFilter, cancellationToken)).Where(grain => grain.GrainKind == GrainKinds.Series).ToList();
        var grainKeys = seriesGrains.ToDictionary(grain => grain.DatasetGrainId, grain => grain.GrainKey);
        var components = await grainFields.ListAsync(new { DatasetGrainId = grainKeys.Keys.ToArray() }, cancellationToken);
        var fieldKeys = (await fields.ListAsync(versionFilter, cancellationToken)).ToDictionary(field => field.FieldId, field => field.FieldKey);
        return
        [
            .. components
                .Where(component => timeFieldIds.Contains(component.FieldId))
                .OrderBy(component => grainKeys[component.DatasetGrainId], StringComparer.Ordinal)
                .ThenBy(component => component.Ordinal)
                .Select(component => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Series grain '{grainKeys[component.DatasetGrainId]}' includes '{FieldName(fieldKeys, component.FieldId)}', " +
                        "the observation-time field of a time binding; a series grain leaves it out."
                })
        ];
    }

    /// <summary>A field's key, or its ID when it is not a field of the version, which <c>grain_fields_in_version</c> reports.</summary>
    private static string FieldName(Dictionary<long, string> fieldKeys, long fieldId)
    {
        if (fieldKeys.TryGetValue(fieldId, out var key)) return key;
        return fieldId.ToString(CultureInfo.InvariantCulture);
    }
}
