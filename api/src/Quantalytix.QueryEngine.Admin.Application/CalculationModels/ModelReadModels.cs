using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Builds the contract's read models of a calculation model from its rows and content.</summary>
public static class ModelReadModels
{
    /// <summary>A model's settings in one version.</summary>
    /// <param name="model">The model row.</param>
    /// <param name="version">The version row.</param>
    /// <param name="liveSnapshotId">The live snapshot.</param>
    /// <returns>The settings.</returns>
    public static CalcModel Settings(CalculationModelRecord model, CalculationModelVersionRecord version, long liveSnapshotId)
    {
        return new CalcModel
        {
            Id = model.CalculationModelId,
            VersionId = version.CalculationModelVersionId,
            SnapshotId = version.SnapshotId,
            IsLive = version.SnapshotId == liveSnapshotId,
            Key = model.ModelKey,
            DisplayName = version.DisplayName,
            CurrencyCode = version.CurrencyCode,
            CalendarKey = version.CalendarKey,
            FormulaLanguage = version.FormulaLanguageVersion,
            AttributionMethod = version.AttributionMethod,
            IsEnabled = version.IsEnabled
        };
    }

    /// <summary>A report as a reader of its model.</summary>
    /// <param name="report">The report with its version.</param>
    /// <returns>The use.</returns>
    public static ModelReportUse Use(VersionedReport report)
    {
        return new ModelReportUse { ReportId = report.ReportId, ReportKey = report.ReportKey, DisplayName = report.Version.DisplayName, IsEnabled = report.Version.IsEnabled };
    }

    /// <summary>The whole model version with the drivers in effect for each metric and the reports that read it.</summary>
    /// <param name="content">The model version's content.</param>
    /// <param name="liveSnapshotId">The live snapshot.</param>
    /// <param name="readers">The reports over the model version, by key.</param>
    /// <returns>The document.</returns>
    public static ModelDocument Document(ModelGraph content, long liveSnapshotId, IEnumerable<VersionedReport> readers)
    {
        return new ModelDocument(
            Settings(content.Model, content.Version, liveSnapshotId),
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
