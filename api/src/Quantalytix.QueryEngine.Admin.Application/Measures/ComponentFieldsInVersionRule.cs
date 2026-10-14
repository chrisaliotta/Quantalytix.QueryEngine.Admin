using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Requires every component field of a measure to be a field of the measure's own dataset version: the query engine
/// resolves components by ID among the version's fields and reads them from the version's source object.
/// </summary>
public sealed class ComponentFieldsInVersionRule(
    ITableGateway<MeasureRecord> measures,
    ITableGateway<MeasureComponentRecord> components,
    ITableGateway<FieldRecord> fields) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "component_fields_in_version";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each component whose field is not a field of the version.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per foreign component field.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionId };
        var measureKeys = (await measures.ListAsync(versionFilter, cancellationToken)).ToDictionary(measure => measure.MeasureId, measure => measure.MeasureKey);
        var rows = await components.ListAsync(new { MeasureId = measureKeys.Keys.ToArray() }, cancellationToken);
        var fieldIds = (await fields.ListAsync(versionFilter, cancellationToken)).Select(field => field.FieldId).ToHashSet();
        return
        [
            .. rows
                .Where(component => !fieldIds.Contains(component.FieldId))
                .OrderBy(component => measureKeys[component.MeasureId], StringComparer.Ordinal)
                .ThenBy(component => component.ComponentRole, StringComparer.Ordinal)
                .Select(component => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Measure '{measureKeys[component.MeasureId]}' reads field {component.FieldId} as its {component.ComponentRole} component, " +
                        "which is not a field of this version."
                })
        ];
    }
}
