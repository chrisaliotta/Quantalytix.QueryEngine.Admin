using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary><c>grain_fields_in_version</c>: every grain component is a field of the grain's version.</summary>
public sealed class GrainFieldsInVersionRule : DatasetRule
{
    /// <summary><c>grain_fields_in_version</c>.</summary>
    protected override string Code => "grain_fields_in_version";

    /// <summary>Reports every component that names no field of the version.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per such component.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var grains = snapshot.Grains.ToDictionary(grain => grain.DatasetGrainId);
        var fieldIds = snapshot.Fields.Select(field => field.FieldId).ToHashSet();
        return snapshot.GrainFields
            .Where(component => !fieldIds.Contains(component.FieldId))
            .OrderBy(component => grains[component.DatasetGrainId].GrainKey, StringComparer.Ordinal)
            .ThenBy(component => component.Ordinal)
            .Select(component =>
            {
                var grain = grains[component.DatasetGrainId];
                return Violation(
                    DatasetTabs.Grains,
                    $"Grain '{grain.GrainKey}' includes field {component.FieldId}, which is not a field of this version.",
                    grain.DatasetGrainId,
                    grain.GrainKey);
            });
    }
}
