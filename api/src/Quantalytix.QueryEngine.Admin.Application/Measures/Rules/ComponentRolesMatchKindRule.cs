using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Requires each measure's component roles to satisfy its kind (schema section 18), as <see cref="MeasureComponentRoles"/>
/// defines them: the query engine reads a measure's value and weight by role, so a missing role fails or silently changes
/// the calculation, and a role the kind does not read is ignored.
/// </summary>
public sealed class ComponentRolesMatchKindRule(ITableGateway<MeasureRecord> measures, ITableGateway<MeasureComponentRecord> components) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "component_roles_match_kind";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each role a measure misses or should not have.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per missing or unexpected role.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionMeasures = await measures.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var roles = (await components.ListAsync(new { MeasureId = versionMeasures.Select(measure => measure.MeasureId).ToArray() }, cancellationToken))
            .ToLookup(component => component.MeasureId, component => component.ComponentRole);
        return
        [
            .. versionMeasures
                .OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal)
                .SelectMany(measure => MeasureComponentRoles.Problems(measure.MeasureKind, measure.AggregationFunction, roles[measure.MeasureId])
                    .Select(problem => new RuleViolation { Rule = Code, Message = $"Measure '{measure.MeasureKey}': {problem}" }))
        ];
    }
}
