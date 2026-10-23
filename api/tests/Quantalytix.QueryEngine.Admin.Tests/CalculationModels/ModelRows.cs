using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>Builds the calculation model rows the model, report and release tests seed, all of model 40 unless changed with <c>with</c>.</summary>
internal static class ModelRows
{
    /// <summary>The calculation model the rows belong to.</summary>
    public const long CalculationModelId = 40;

    /// <summary>Model 40, in US dollars, on formula language 1.0 with Shapley attribution.</summary>
    public static CalculationModelRecord Model()
    {
        return new CalculationModelRecord
        {
            CalculationModelId = CalculationModelId,
            ModelKey = "margin",
            DisplayName = "Margin",
            CalendarKey = "gregorian",
            CurrencyCode = "USD",
            FormulaLanguageVersion = FormulaLanguageVersions.Version10,
            IsEnabled = true,
            AttributionMethod = AttributionMethods.Shapley
        };
    }

    /// <summary>
    /// The seed's margin model in miniature: variables <c>AVERAGE_BALANCE</c>, <c>INTEREST_INCOME</c> and <c>INTEREST_EXPENSE</c>,
    /// calculated measure <c>YIELD</c> annualized by the context parameters, and two attributions as the seed has them: <c>YIELD</c>
    /// by its formula with drivers income, volume and days, weighed by <c>AVERAGE_BALANCE</c>, and <c>INTEREST_INCOME</c> by an
    /// identity expression with drivers volume, rate and days.
    /// </summary>
    public static ModelGraph MarginGraph()
    {
        return Graph(
            [Variable(51, "AVERAGE_BALANCE"), Variable(52, "INTEREST_INCOME"), Variable(53, "INTEREST_EXPENSE")],
            [Calculated(61, "YIELD", "[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] * @yearDays / @periodDays")],
            Attribution(
                "YIELD", null,
                Driver("income", 1, "INTEREST_INCOME"), Driver("volume", 2, "AVERAGE_BALANCE"), Driver("days", 3, "@periodDays", "@yearDays"))
                with { WeightVariableKey = "AVERAGE_BALANCE" },
            Attribution(
                "INTEREST_INCOME", "[m.AVERAGE_BALANCE] * [m.YIELD] * @periodDays / @yearDays",
                Driver("volume", 1, "AVERAGE_BALANCE"), Driver("rate", 2, "YIELD"), Driver("days", 3, "@periodDays", "@yearDays")));
    }

    /// <summary>The margin model with one attribution in place of the one for the same metric.</summary>
    public static ModelGraph MarginGraphWith(MetricAttribution attribution)
    {
        return MetricAttributionService.Put(MarginGraph(), attribution);
    }

    /// <summary>
    /// The seed's margin model after its move to formula language 1.2: <see cref="MarginGraph"/> with the day count parameter, and
    /// YIELD and the INTEREST_INCOME identity annualized by <c>YEARFRAC(@dayCount)</c>, while their days drivers still switch
    /// <c>@periodDays</c> and <c>@yearDays</c>.
    /// </summary>
    public static ModelGraph YearFracGraph()
    {
        var model = MarginGraph();
        return model with
        {
            Model = model.Model with { FormulaLanguageVersion = FormulaLanguageVersions.Version12 },
            CalculatedMeasures = [Calculated(61, "YIELD", "[m.INTEREST_INCOME] / [m.AVERAGE_BALANCE] / YEARFRAC(@dayCount)")],
            Attributions = [model.Attributions[0], model.Attributions[1] with { ExpressionText = "[m.AVERAGE_BALANCE] * [m.YIELD] * YEARFRAC(@dayCount)" }],
            Parameters = [DayCount()]
        };
    }

    /// <summary>The seed's day count parameter: text limited to the day count conventions, which defaults to actual/actual.</summary>
    public static ModelParameter DayCount()
    {
        return TextParameter("dayCount", DayCountConventions.ActualActual) with { AllowedValues = [.. DayCountConventions.Definition.Values.Select(value => value.Value)] };
    }

    /// <summary>A text parameter that takes any text, named by its key.</summary>
    public static ModelParameter TextParameter(string parameterKey, string defaultText)
    {
        return new ModelParameter { ParameterKey = parameterKey, DisplayName = parameterKey, ValueType = ParameterValueTypes.Text, DefaultText = defaultText };
    }

    /// <summary>A number parameter, named by its key.</summary>
    public static ModelParameter NumberParameter(string parameterKey, decimal defaultNumber)
    {
        return new ModelParameter { ParameterKey = parameterKey, DisplayName = parameterKey, ValueType = ParameterValueTypes.Number, DefaultNumber = defaultNumber };
    }

    /// <summary>The model's metrics as <see cref="ModelChanges"/> checks them: model 40 with the given rows.</summary>
    public static ModelGraph Graph(
        IReadOnlyList<VariableRecord> variables, IReadOnlyList<CalculatedMeasureRecord> measures, params IReadOnlyList<MetricAttribution> attributions)
    {
        return new ModelGraph { Model = Model(), Variables = variables, CalculatedMeasures = measures, Attributions = attributions, Parameters = [] };
    }

    /// <summary>An attribution of a metric with an optional expression and the given drivers, without a weight variable.</summary>
    public static MetricAttribution Attribution(string metricKey, string? expressionText, params IReadOnlyList<AttributionDriver> drivers)
    {
        return new MetricAttribution { MetricKey = metricKey, ExpressionText = expressionText, Drivers = drivers };
    }

    /// <summary>A driver that switches the given inputs, labelled by its key.</summary>
    public static AttributionDriver Driver(string driverKey, int sortOrder, params IReadOnlyList<string> inputKeys)
    {
        return new AttributionDriver { DriverKey = driverKey, DisplayName = driverKey, SortOrder = sortOrder, InputKeys = inputKeys };
    }

    /// <summary>A currency variable that reads missing values as zero.</summary>
    public static VariableRecord Variable(long variableId, string variableKey)
    {
        return new VariableRecord
        {
            VariableId = variableId,
            CalculationModelId = CalculationModelId,
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
            CalculationModelId = CalculationModelId,
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
