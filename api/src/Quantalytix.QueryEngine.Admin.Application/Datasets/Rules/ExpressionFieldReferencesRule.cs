using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Requires every <c>[f.key]</c> in an expression field to name a field of the same dataset version, because the query
/// engine renders each reference by looking the key up among the version's fields.
/// </summary>
public sealed class ExpressionFieldReferencesRule(ITableGateway<FieldRecord> fields) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "expression_field_references";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each field reference of an expression field that names no field of the version.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per unknown reference.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await fields.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var keys = rows.Select(field => field.FieldKey).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. rows
                .Where(field => field.FieldSourceKind == FieldSourceKinds.Expression)
                .OrderBy(field => field.FieldKey, StringComparer.Ordinal)
                .SelectMany(field => ExpressionInspector.Inspect(field.ExpressionText!).FieldKeys
                    .Where(key => !keys.Contains(key))
                    .Select(key => new RuleViolation
                    {
                        Rule = Code,
                        Message = $"Expression field '{field.FieldKey}' references '{key}', which is not a field of this version."
                    }))
        ];
    }
}
