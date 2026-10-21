using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The variable and calculated measure keys of a calculation model, which report columns and lines name.</summary>
public sealed record ModelMetrics
{
    /// <summary>The model's variable keys.</summary>
    public required IReadOnlySet<string> VariableKeys { get; init; }

    /// <summary>The model's calculated measure keys.</summary>
    public required IReadOnlySet<string> CalculatedMeasureKeys { get; init; }

    /// <summary>Whether the model has a metric of a value kind with a key.</summary>
    /// <param name="valueKind">A <c>report_value_kind</c> value.</param>
    /// <param name="key">A metric key.</param>
    /// <returns>True when the model has the metric.</returns>
    public bool Has(string valueKind, string key)
    {
        return valueKind == ReportValueKinds.Variable ? VariableKeys.Contains(key) : CalculatedMeasureKeys.Contains(key);
    }
}
