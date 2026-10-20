using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The variables each metric of a model version reads: a variable reads itself, and a calculated measure every variable its
/// formula reaches, directly or through other calculated measures. Each measure is followed once, so a version whose formulas
/// <c>formulas_acyclic</c> reports as cyclic still yields an answer.
/// </summary>
public sealed class FormulaGraph
{
    /// <summary>The version's variable keys.</summary>
    private readonly HashSet<string> _variables;

    /// <summary>The metric keys each calculated measure's formula reads, by measure key.</summary>
    private readonly Dictionary<string, IReadOnlyList<string>> _reads;

    /// <summary>Parses the version's formulas once.</summary>
    /// <param name="variables">The version's variables.</param>
    /// <param name="measures">The version's calculated measures.</param>
    public FormulaGraph(IEnumerable<VariableRecord> variables, IEnumerable<CalculatedMeasureRecord> measures)
    {
        _variables = variables.Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal);
        _reads = measures.ToDictionary(measure => measure.MeasureKey, measure => ExpressionInspector.Inspect(measure.ExpressionText).MeasureKeys, StringComparer.Ordinal);
    }

    /// <summary>The calculated measure keys of the version.</summary>
    public IEnumerable<string> MeasureKeys => _reads.Keys;

    /// <summary>The variables a metric reads; a key that names no metric of the version reads none.</summary>
    /// <param name="metricKey">A variable or calculated measure key.</param>
    /// <returns>The variable keys.</returns>
    public IReadOnlySet<string> Variables(string metricKey)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var followed = new HashSet<string>(StringComparer.Ordinal);
        Follow(metricKey);
        return reached;

        void Follow(string key)
        {
            if (_variables.Contains(key)) reached.Add(key);
            else if (_reads.TryGetValue(key, out var reads) && followed.Add(key))
                foreach (var read in reads) Follow(read);
        }
    }
}
