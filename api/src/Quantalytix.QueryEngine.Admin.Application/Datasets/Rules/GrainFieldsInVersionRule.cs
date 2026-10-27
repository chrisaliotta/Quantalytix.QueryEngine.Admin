using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Requires every component field of a grain to be a field of the grain's own dataset version (schema section 7).</summary>
public sealed class GrainFieldsInVersionRule(
    ITableGateway<DatasetGrainRecord> grains,
    ITableGateway<DatasetGrainFieldRecord> grainFields,
    ITableGateway<FieldRecord> fields) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "grain_fields_in_version";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each grain component whose field belongs to no field of the version.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per foreign component field.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionGrains = await grains.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var grainKeys = versionGrains.ToDictionary(grain => grain.DatasetGrainId, grain => grain.GrainKey);
        var components = await grainFields.ListAsync(new { DatasetGrainId = grainKeys.Keys.ToArray() }, cancellationToken);
        var versionFields = await fields.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var fieldIds = versionFields.Select(field => field.FieldId).ToHashSet();
        return
        [
            .. components
                .Where(component => !fieldIds.Contains(component.FieldId))
                .OrderBy(component => grainKeys[component.DatasetGrainId], StringComparer.Ordinal)
                .ThenBy(component => component.Ordinal)
                .Select(component => new RuleViolation
                {
                    Code = Code,
                    Message = $"Grain '{grainKeys[component.DatasetGrainId]}' includes field {component.FieldId}, which is not a field of this version."
                })
        ];
    }
}
