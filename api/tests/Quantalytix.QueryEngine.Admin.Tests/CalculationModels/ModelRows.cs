using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>Builds the calculation model rows the model, report and release tests seed, all of model 40 unless changed with <c>with</c>.</summary>
internal static class ModelRows
{
    /// <summary>The calculation model the rows belong to.</summary>
    public const long ModelId = 40;

    /// <summary>Model 40, in US dollars.</summary>
    public static CalculationModelRecord Model()
    {
        return new CalculationModelRecord
        {
            ModelId = ModelId,
            ModelKey = "margin",
            DisplayName = "Margin",
            CalendarKey = "gregorian",
            CurrencyCode = "USD",
            FormulaLanguageVersion = "1.0",
            IsEnabled = true
        };
    }

    /// <summary>A currency variable that reads missing values as zero.</summary>
    public static VariableRecord Variable(long variableId, string variableKey)
    {
        return new VariableRecord
        {
            VariableId = variableId,
            ModelId = ModelId,
            VariableKey = variableKey,
            DisplayName = variableKey,
            VariableRole = "input",
            MissingBehavior = VariableMissingBehaviors.Zero,
            DisplayFormat = "currency"
        };
    }

    /// <summary>A decimal calculated measure.</summary>
    public static CalculatedMeasureRecord Calculated(long measureId, string measureKey, string expressionText)
    {
        return new CalculatedMeasureRecord
        {
            CalculatedMeasureId = measureId,
            ModelId = ModelId,
            MeasureKey = measureKey,
            DisplayName = measureKey,
            ExpressionText = expressionText,
            OutputDataType = MeasureOutputDataTypes.Decimal,
            OutputPrecision = 20,
            OutputScale = 4,
            DisplayFormat = "percent"
        };
    }
}
