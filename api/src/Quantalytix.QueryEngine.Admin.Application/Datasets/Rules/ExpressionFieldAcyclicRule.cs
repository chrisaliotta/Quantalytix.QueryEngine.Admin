using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary><c>expression_field_acyclic</c>: no expression field reaches itself through its field references (D7).</summary>
public sealed class ExpressionFieldAcyclicRule : DatasetRule
{
    /// <summary><c>expression_field_acyclic</c>.</summary>
    protected override string Code => "expression_field_acyclic";

    /// <summary>Reports every expression field on a cycle.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per field on a cycle.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var expressions = snapshot.Fields.Where(field => field.FieldSourceKind == FieldSourceKinds.Expression).ToList();
        var references = expressions
            .GroupBy(field => field.FieldKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.SelectMany(field => ExpressionInspector.Inspect(field.ExpressionText!).FieldKeys).ToList(), StringComparer.Ordinal);
        return expressions
            .OrderBy(field => field.FieldKey, StringComparer.Ordinal)
            .Where(field => ReachesItself(field.FieldKey, references))
            .Select(field => Violation(DatasetTabs.Fields, $"Expression field '{field.FieldKey}' references itself through its field references.", field.FieldId, field.FieldKey));
    }

    /// <summary>Whether a field's references lead back to it.</summary>
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
