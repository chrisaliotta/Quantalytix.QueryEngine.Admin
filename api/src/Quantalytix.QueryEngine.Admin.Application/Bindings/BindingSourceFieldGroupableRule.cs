using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires the source field of a groupable binding to be groupable itself (schema section 11): the field flag permits
/// grouping at all, the binding flag only whether its semantic route groups, and the query engine refuses to group by a
/// field that is not groupable before it looks at the binding.
/// </summary>
public sealed class BindingSourceFieldGroupableRule(ITableGateway<DimensionBindingRecord> bindings, ITableGateway<FieldRecord> fields)
    : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "binding_source_field_groupable";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each groupable binding whose source field is a field of the version that is not groupable.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per groupable binding over a field that is not groupable.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionId };
        var versionBindings = await bindings.ListAsync(versionFilter, cancellationToken);
        var versionFields = await fields.ListAsync(versionFilter, cancellationToken);
        return
        [
            .. versionBindings
                .Where(binding => binding.IsGroupable)
                .Join(versionFields, binding => binding.SourceFieldId, field => (long?)field.FieldId, (binding, field) => (Binding: binding, Field: field))
                .Where(pair => !pair.Field.IsGroupable)
                .OrderBy(pair => pair.Binding.DisplayName, StringComparer.Ordinal)
                .Select(pair => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Binding '{pair.Binding.DisplayName}' is groupable, but its source field '{pair.Field.FieldKey}' is not; " +
                        "make the field groupable or the binding not groupable."
                })
        ];
    }
}
