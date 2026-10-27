using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Builds the contract's read models of a calculation model from its rows and content.</summary>
public static class ModelReadModels
{
    /// <summary>A model's settings.</summary>
    /// <param name="model">The model row.</param>
    /// <returns>The settings.</returns>
    public static CalcModel Settings(CalculationModelRecord model)
    {
        return new CalcModel
        {
            Id = model.CalculationModelId,
            Key = model.ModelKey,
            DisplayName = model.DisplayName,
            CurrencyCode = model.CurrencyCode,
            CalendarKey = model.CalendarKey,
            FormulaLanguage = model.FormulaLanguageVersion,
            AttributionMethod = model.AttributionMethod,
            IsEnabled = model.IsEnabled
        };
    }

    /// <summary>A report as a reader of its model.</summary>
    /// <param name="report">The report row.</param>
    /// <returns>The use.</returns>
    public static ModelReportUse Use(ReportRecord report)
    {
        return new ModelReportUse { ReportId = report.ReportId, ReportKey = report.ReportKey, DisplayName = report.DisplayName, IsEnabled = report.IsEnabled };
    }

    /// <summary>The whole model with the drivers in effect for each metric and the reports that read it.</summary>
    /// <param name="content">The model's content.</param>
    /// <param name="readers">The reports over the model, by key.</param>
    /// <returns>The document.</returns>
    public static ModelDocument Document(ModelGraph content, IEnumerable<ReportRecord> readers)
    {
        return new ModelDocument(
            Settings(content.Model),
            [.. content.Variables.Select(Variable)],
            [.. content.CalculatedMeasures.Select(Measure)],
            [.. content.Parameters.Select(Parameter)],
            content.Attributions,
            EffectiveDrivers(content),
            [.. readers.Select(Use)]);
    }

    /// <summary>
    /// The drivers in effect per metric: an attribution's own, or else the default drivers of its expression, which is the attribution's
    /// or a calculated measure's formula. A variable without an attribution has no formula to split, so it has no entry.
    /// </summary>
    /// <param name="content">The model's content.</param>
    /// <returns>The drivers by metric key.</returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<AttributionDriver>> EffectiveDrivers(ModelGraph content)
    {
        var explicitly = content.Attributions.ToDictionary(attribution => attribution.MetricKey, StringComparer.Ordinal);
        var drivers = new Dictionary<string, IReadOnlyList<AttributionDriver>>(StringComparer.Ordinal);
        foreach (var key in content.Variables.Select(variable => variable.VariableKey).Concat(content.CalculatedMeasures.Select(measure => measure.MeasureKey)))
        {
            var attribution = explicitly.GetValueOrDefault(key);
            if (attribution is { Drivers.Count: > 0 }) drivers[key] = attribution.Drivers;
            else if (attribution is not null && AttributionInputs.ExpressionOf(content, attribution) is { } expression) drivers[key] = AttributionInputs.DefaultDrivers(content, expression);
            else if (content.CalculatedMeasures.FirstOrDefault(measure => measure.MeasureKey == key) is { } measure) drivers[key] = AttributionInputs.DefaultDrivers(content, measure.ExpressionText);
        }

        return drivers;
    }

    /// <summary>A variable row as the contract shows it.</summary>
    private static ModelVariable Variable(VariableRecord variable)
    {
        return new ModelVariable
        {
            Key = variable.VariableKey,
            DisplayName = variable.DisplayName,
            DisplayFormat = variable.DisplayFormat,
            UnitKey = variable.UnitKey,
            MissingBehavior = variable.MissingBehavior
        };
    }

    /// <summary>A calculated measure row as the contract shows it.</summary>
    private static ModelMeasure Measure(CalculatedMeasureRecord measure)
    {
        return new ModelMeasure
        {
            Key = measure.MeasureKey,
            DisplayName = measure.DisplayName,
            Formula = measure.ExpressionText,
            OutputDataType = measure.OutputDataType,
            OutputPrecision = measure.OutputPrecision,
            OutputScale = measure.OutputScale,
            RoundingMode = measure.RoundingMode,
            UnitKey = measure.UnitKey,
            DisplayFormat = measure.DisplayFormat
        };
    }

    /// <summary>A parameter as the contract shows it.</summary>
    private static ModelParameterItem Parameter(ModelParameter parameter)
    {
        return new ModelParameterItem
        {
            Key = parameter.ParameterKey,
            DisplayName = parameter.DisplayName,
            ValueType = parameter.ValueType,
            AllowedValues = parameter.AllowedValues,
            DefaultNumber = parameter.DefaultNumber,
            DefaultText = parameter.DefaultText,
            Description = parameter.Description
        };
    }
}
