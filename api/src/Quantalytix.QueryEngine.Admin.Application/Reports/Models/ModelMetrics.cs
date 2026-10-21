using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The variable and calculated measure keys of a model's published version, which report columns and lines name.</summary>
public sealed record ModelMetrics
{
    /// <summary>The version's variable keys.</summary>
    public required IReadOnlySet<string> VariableKeys { get; init; }

    /// <summary>The version's calculated measure keys.</summary>
    public required IReadOnlySet<string> CalculatedMeasureKeys { get; init; }

    /// <summary>Whether the version has a metric of a value kind with a key.</summary>
    /// <param name="valueKind">A <c>report_value_kind</c> value.</param>
    /// <param name="key">A metric key.</param>
    /// <returns>True when the version has the metric.</returns>
    public bool Has(string valueKind, string key)
    {
        return valueKind == ReportValueKinds.Variable ? VariableKeys.Contains(key) : CalculatedMeasureKeys.Contains(key);
    }
}
