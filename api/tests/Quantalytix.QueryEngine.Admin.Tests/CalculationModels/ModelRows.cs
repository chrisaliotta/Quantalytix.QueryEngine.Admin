using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>Builds the calculation model rows the model, report and release tests seed, all in model version 41 unless changed with <c>with</c>.</summary>
internal static class ModelRows
{
    /// <summary>The calculation model the rows belong to.</summary>
    public const long ModelId = 40;

    /// <summary>The model version the rows belong to.</summary>
    public const long ModelVersionId = 41;

    /// <summary>A version of model 40, published unless told otherwise.</summary>
    public static CalculationModelVersionRecord ModelVersion(long modelVersionId = ModelVersionId, string status = PublicationStatuses.Published)
    {
        return new CalculationModelVersionRecord
        {
            ModelVersionId = modelVersionId,
            ModelId = ModelId,
            VersionNumber = (int)(modelVersionId % 10),
            Status = status,
            CalendarKey = "gregorian",
            CurrencyCode = "USD",
            FormulaLanguageVersion = "1.0"
        };
    }

    /// <summary>A currency variable that reads missing values as zero.</summary>
    public static VariableRecord Variable(long variableId, string variableKey)
    {
        return new VariableRecord
        {
            VariableId = variableId,
            ModelVersionId = ModelVersionId,
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
            ModelVersionId = ModelVersionId,
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
