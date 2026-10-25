using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>formulas_acyclic</c>: no calculated measure reaches itself through the calculated measures its formula reads, because the
/// orchestrator evaluates each formula after the ones it reads, which a cycle makes impossible.
/// </summary>
public static class FormulasAcyclicRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "formulas_acyclic";

    /// <summary>Reports each calculated measure on a cycle.</summary>
    /// <param name="model">The model's metrics.</param>
    /// <returns>One violation per calculated measure that reaches itself.</returns>
    public static IEnumerable<RuleViolation> Evaluate(ModelGraph model)
    {
        var reads = model.CalculatedMeasures.ToDictionary(
            measure => measure.MeasureKey,
            measure => ExpressionInspector.Inspect(measure.ExpressionText).MeasureKeys,
            StringComparer.Ordinal);
        return reads.Keys.Order(StringComparer.Ordinal).Where(key => Reaches(key, key, reads, [])).Select(key => new RuleViolation
        {
            Rule = Code,
            Message = $"Calculated measure '{key}' reads itself through the calculated measures its formula reads."
        });
    }

    /// <summary>Whether a calculated measure's formula reaches the target through calculated measures.</summary>
    private static bool Reaches(string from, string target, IReadOnlyDictionary<string, IReadOnlyList<string>> reads, HashSet<string> visited)
    {
        foreach (var key in reads[from].Where(reads.ContainsKey))
        {
            if (key == target) return true;
            if (visited.Add(key) && Reaches(key, target, reads, visited)) return true;
        }
        return false;
    }
}
