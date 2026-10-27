using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary><c>formula_acyclic</c> (D7): the formula dependency graph has no cycle; each cycle is reported once, at its first measure.</summary>
public sealed class FormulaAcyclicRule : MeasureRule
{
    /// <summary><c>formula_acyclic</c>.</summary>
    protected override string Code => "formula_acyclic";

    /// <summary>Reports every cycle of the dependencies.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per cycle.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var edges = snapshot.Dependencies.ToLookup(dependency => dependency.MeasureId, dependency => dependency.DependencyMeasureId);
        var reachable = snapshot.Measures.ToDictionary(measure => measure.MeasureId, measure => Reachable(measure.MeasureId, edges));
        var onCycle = ByKey(snapshot).Where(measure => reachable[measure.MeasureId].Contains(measure.MeasureId)).ToList();
        var reported = new HashSet<long>();
        foreach (var measure in onCycle.Where(measure => !reported.Contains(measure.MeasureId)))
        {
            var cycle = onCycle.Where(other => reachable[measure.MeasureId].Contains(other.MeasureId) && reachable[other.MeasureId].Contains(measure.MeasureId)).ToList();
            reported.UnionWith(cycle.Select(member => member.MeasureId));
            yield return Violation(measure, CycleMessage([.. cycle.Select(member => member.MeasureKey)]));
        }
    }

    /// <summary>The measures a measure's formula reaches.</summary>
    private static HashSet<long> Reachable(long measureId, ILookup<long, long> edges)
    {
        var reached = new HashSet<long>();
        var pending = new Stack<long>(edges[measureId]);
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            if (!reached.Add(next)) continue;
            foreach (var dependency in edges[next]) pending.Push(dependency);
        }

        return reached;
    }

    /// <summary>The message naming a cycle's measures.</summary>
    private static string CycleMessage(List<string> measureKeys)
    {
        if (measureKeys.Count == 1) return $"Formula measure '{measureKeys[0]}' references itself.";
        return $"Formula measures {string.Join(", ", measureKeys.Select(key => $"'{key}'"))} depend on each other in a cycle.";
    }
}
