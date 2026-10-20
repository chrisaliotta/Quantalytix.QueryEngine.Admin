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

    /// <summary>A variable that sums the descendants of its target member, reading missing values as zero.</summary>
    public static VariableRecord Variable(long variableId, string variableKey, string? targetMemberKey = null)
    {
        return new VariableRecord
        {
            VariableId = variableId,
            ModelVersionId = ModelVersionId,
            VariableKey = variableKey,
            DisplayName = variableKey,
            VariableRole = "input",
            TargetMemberKey = targetMemberKey,
            SelectionScope = SelectionScopes.Descendants,
            MissingBehavior = VariableMissingBehaviors.Zero
        };
    }

    /// <summary>An enabled source of a variable that reads <c>balance</c> by <c>gl_account</c> and <c>as_of_date</c>.</summary>
    public static VariableSourceRecord Source(long sourceId, long variableId, string sourceKey, long datasetId)
    {
        return new VariableSourceRecord
        {
            VariableSourceId = sourceId,
            ModelVersionId = ModelVersionId,
            VariableId = variableId,
            SourceKey = sourceKey,
            DatasetId = datasetId,
            ReportFieldKey = "gl_account",
            TimeFieldKey = "as_of_date",
            MeasureKey = "balance",
            IsEnabled = true
        };
    }

    /// <summary>An enabled source profile.</summary>
    public static SourceProfileRecord Profile(long profileId, string profileKey)
    {
        return new SourceProfileRecord { SourceProfileId = profileId, ModelVersionId = ModelVersionId, ProfileKey = profileKey, DisplayName = profileKey, IsEnabled = true };
    }

    /// <summary>A profile's pick of a source for a variable.</summary>
    public static SourceProfileVariableRecord Pick(long profileId, long variableId, long sourceId)
    {
        return new SourceProfileVariableRecord { SourceProfileId = profileId, VariableId = variableId, VariableSourceId = sourceId };
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

    /// <summary>An enabled, groupable and filterable calculation dimension over a canonical dimension.</summary>
    public static CalculationDimensionRecord CalculationDimension(long calculationDimensionId, string dimensionKey, long dimensionId)
    {
        return new CalculationDimensionRecord
        {
            CalculationDimensionId = calculationDimensionId,
            ModelVersionId = ModelVersionId,
            DimensionKey = dimensionKey,
            DisplayName = dimensionKey,
            ValueKind = CalculationValueKinds.Canonical,
            DimensionId = dimensionId,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true
        };
    }

    /// <summary>A dataset's default route of a calculation dimension; set <c>VariableSourceId</c> for a source override.</summary>
    public static CalculationRouteRecord Route(long routeId, long calculationDimensionId, long datasetId, string fieldKey, string routeKind = RouteKinds.Semantic)
    {
        return new CalculationRouteRecord
        {
            CalculationBindingId = routeId,
            ModelVersionId = ModelVersionId,
            CalculationDimensionId = calculationDimensionId,
            DatasetId = datasetId,
            RouteKind = routeKind,
            FieldKey = fieldKey
        };
    }
}
