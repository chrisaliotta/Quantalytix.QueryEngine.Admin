using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The variables and calculated measures of one model version, by key and by ID, as report columns and lines name them.</summary>
public sealed record ModelMetrics
{
    /// <summary>The model version.</summary>
    public required long ModelVersionId { get; init; }

    /// <summary>The version's variables.</summary>
    public required IReadOnlyList<VariableRecord> Variables { get; init; }

    /// <summary>The version's calculated measures.</summary>
    public required IReadOnlyList<CalculatedMeasureRecord> CalculatedMeasures { get; init; }

    /// <summary>The ID of the variable or calculated measure a key names, as the value kind says, or null.</summary>
    /// <param name="valueKind">A <c>report_value_kind</c> value.</param>
    /// <param name="key">A metric key.</param>
    /// <returns>The metric's ID, or null when the version has no such metric.</returns>
    public long? IdOf(string valueKind, string key)
    {
        return valueKind == ReportValueKinds.Variable
            ? Variables.SingleOrDefault(variable => variable.VariableKey == key)?.VariableId
            : CalculatedMeasures.SingleOrDefault(measure => measure.MeasureKey == key)?.CalculatedMeasureId;
    }

    /// <summary>The key of a column's metric.</summary>
    /// <param name="column">A column of a report version over this model version.</param>
    /// <returns>The variable or calculated measure key.</returns>
    public string KeyOf(ReportColumnRecord column)
    {
        return column.ValueKind == ReportValueKinds.Variable
            ? Variables.Single(variable => variable.VariableId == column.VariableId).VariableKey
            : CalculatedMeasures.Single(measure => measure.CalculatedMeasureId == column.CalculatedMeasureId).MeasureKey;
    }
}
