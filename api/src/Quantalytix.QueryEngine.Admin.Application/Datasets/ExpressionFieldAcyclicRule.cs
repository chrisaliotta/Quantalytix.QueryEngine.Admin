using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Forbids expression fields that reference each other in a cycle, directly or through other expression fields: the query
/// engine renders a reference by rendering the referenced field, so a cycle never ends.
/// </summary>
public sealed class ExpressionFieldAcyclicRule(ITableGateway<FieldRecord> fields) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "expression_field_acyclic";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each expression field whose references lead back to itself.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per expression field on a cycle.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await fields.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var references = rows
            .Where(field => field.FieldSourceKind == FieldSourceKinds.Expression)
            .GroupBy(field => field.FieldKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(field => ExpressionInspector.Inspect(field.ExpressionText!).FieldKeys).ToList(),
                StringComparer.Ordinal);
        return
        [
            .. references.Keys
                .Order(StringComparer.Ordinal)
                .Where(key => ReachesItself(key, references))
                .Select(key => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Expression field '{key}' references itself through its field references."
                })
        ];
    }

    /// <summary>Whether following the references of expression fields from a field leads back to it.</summary>
    private static bool ReachesItself(string key, Dictionary<string, List<string>> references)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(references[key]);
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            if (next == key) return true;
            if (!visited.Add(next) || !references.TryGetValue(next, out var nextReferences)) continue;
            foreach (var reference in nextReferences) pending.Push(reference);
        }

        return false;
    }
}
