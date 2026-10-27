using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Forbids cycles and self-references in a dataset version's formula dependencies (schema sections 13 and 18): the query
/// engine evaluates a formula after the measures it depends on, so measures that depend on each other can never be
/// evaluated. Reads the derived <c>qe_measure_dependency</c> rows, which are the graph the engine follows.
/// </summary>
public sealed class FormulaAcyclicRule(ITableGateway<MeasureRecord> measures, ITableGateway<MeasureDependencyRecord> dependencies) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "formula_acyclic";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each group of measures that depend on each other, once, and each measure that depends on itself.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per cycle.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var keys = (await measures.ListAsync(new { DatasetVersionId = versionId }, cancellationToken)).ToDictionary(measure => measure.MeasureId, measure => measure.MeasureKey);
        var edges = (await dependencies.ListAsync(new { MeasureId = keys.Keys.ToArray() }, cancellationToken))
            .ToLookup(dependency => dependency.MeasureId, dependency => dependency.DependencyMeasureId);
        var reachable = keys.Keys.ToDictionary(measureId => measureId, measureId => Reachable(measureId, edges));
        var onCycle = keys.Keys
            .Where(measureId => reachable[measureId].Contains(measureId))
            .OrderBy(measureId => keys[measureId], StringComparer.Ordinal)
            .ToList();

        var reported = new HashSet<long>();
        var violations = new List<RuleViolation>();
        foreach (var measureId in onCycle)
        {
            if (reported.Contains(measureId)) continue;
            var cycle = onCycle.Where(other => reachable[measureId].Contains(other) && reachable[other].Contains(measureId)).ToList();
            reported.UnionWith(cycle);
            violations.Add(new RuleViolation { Code = Code, Message = CycleMessage(cycle.Select(member => keys[member]).ToList()) });
        }

        return violations;
    }

    /// <summary>The measures a measure depends on through one or more dependency rows.</summary>
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

    /// <summary>The message of a cycle, naming its measures in key order.</summary>
    private static string CycleMessage(List<string> measureKeys)
    {
        if (measureKeys.Count == 1) return $"Formula measure '{measureKeys[0]}' references itself.";
        return $"Formula measures {string.Join(", ", measureKeys.Select(key => $"'{key}'"))} depend on each other in a cycle.";
    }
}
