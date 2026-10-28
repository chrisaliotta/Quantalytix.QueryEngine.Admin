using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary><c>expression_field_references</c>: every field an expression field references (<c>[f.key]</c>) is a field of the version.</summary>
public sealed class ExpressionFieldReferencesRule : DatasetRule
{
    /// <summary><c>expression_field_references</c>.</summary>
    protected override string Code => "expression_field_references";

    /// <summary>Reports every unknown field reference.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per unknown reference.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var keys = snapshot.Fields.Select(field => field.FieldKey).ToHashSet(StringComparer.Ordinal);
        return snapshot.Fields
            .Where(field => field.FieldSourceKind == FieldSourceKinds.Expression)
            .OrderBy(field => field.FieldKey, StringComparer.Ordinal)
            .SelectMany(field => ExpressionInspector.Inspect(field.ExpressionText!).FieldKeys
                .Where(key => !keys.Contains(key))
                .Select(key => Violation(
                    DatasetTabs.Fields,
                    $"Expression field '{field.FieldKey}' references '{key}', which is not a field of this version.",
                    field.FieldId,
                    field.FieldKey)));
    }
}
