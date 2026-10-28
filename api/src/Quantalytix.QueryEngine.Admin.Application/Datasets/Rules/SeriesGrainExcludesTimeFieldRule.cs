using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// <c>series_grain_excludes_time_field</c>: a series grain leaves out the observation-time field of every time binding, because a
/// series is the set of rows that differ only in time.
/// </summary>
public sealed class SeriesGrainExcludesTimeFieldRule : DatasetRule
{
    /// <summary><c>series_grain_excludes_time_field</c>.</summary>
    protected override string Code => "series_grain_excludes_time_field";

    /// <summary>Reports every series grain component that is a time binding's source field.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per such component.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var timeBindingIds = snapshot.TimeBindings.Select(time => time.DimensionBindingId).ToHashSet();
        var timeFieldIds = snapshot.Bindings
            .Where(binding => timeBindingIds.Contains(binding.DimensionBindingId) && binding.SourceFieldId is not null)
            .Select(binding => binding.SourceFieldId!.Value)
            .ToHashSet();
        var series = snapshot.Grains.Where(grain => grain.GrainKind == GrainKinds.Series).ToDictionary(grain => grain.DatasetGrainId);
        var fieldKeys = snapshot.Fields.ToDictionary(field => field.FieldId, field => field.FieldKey);
        return snapshot.GrainFields
            .Where(component => series.ContainsKey(component.DatasetGrainId) && timeFieldIds.Contains(component.FieldId))
            .OrderBy(component => series[component.DatasetGrainId].GrainKey, StringComparer.Ordinal)
            .ThenBy(component => component.Ordinal)
            .Select(component =>
            {
                var grain = series[component.DatasetGrainId];
                return Violation(
                    DatasetTabs.Grains,
                    $"Series grain '{grain.GrainKey}' includes '{fieldKeys[component.FieldId]}', the observation-time field of a time binding; a series grain leaves it out.",
                    grain.DatasetGrainId,
                    grain.GrainKey);
            });
    }
}
