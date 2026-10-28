using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Measures;

/// <summary>
/// Confirms the measure input rules of schema sections 13, 14 and 18: each measure kind's aggregation function, component
/// roles, formula, time policy and implementation; formulas that read measures only and row filters that read fields
/// only, both parsed by the query engine; decimal outputs with a precision and scale; accumulators StarRocks supports; and
/// a finite lookback for every boundary match that searches back for an observation. The engine computes measures from
/// exactly these columns, so a measure that breaks them fails or miscalculates at query time.
/// </summary>
public sealed class MeasureInputValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly DatasetMeasureInputValidator _validator = new();

    /// <summary>A valid measure of each kind passes.</summary>
    /// <param name="kind">The measure's kind.</param>
    [Theory]
    [InlineData(MeasureKinds.Aggregate)]
    [InlineData(MeasureKinds.WeightedSum)]
    [InlineData(MeasureKinds.WeightedAverage)]
    [InlineData(MeasureKinds.PeriodAverage)]
    [InlineData(MeasureKinds.Formula)]
    [InlineData(MeasureKinds.Specialized)]
    public void ValidMeasureOfEachKind_Passes(string kind)
    {
        _validator.TestValidate(Measure(kind)).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>An aggregate needs an aggregation function and a value component.</summary>
    [Fact]
    public void Aggregate_RequiresAFunctionAndAValueComponent()
    {
        var input = Measure(MeasureKinds.Aggregate) with { AggregationFunction = null, Components = [] };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(measure => measure.AggregationFunction);
        result.ShouldHaveValidationErrorFor(measure => measure.Components).WithErrorMessage("'aggregate' measures need a 'value' component.");
    }

    /// <summary>
    /// A count without a value component counts rows, as the query engine compiles it, so a row count such as the seed kit's
    /// <c>rowCount</c> is valid; a distinct count still needs the value whose distinct entries it counts.
    /// </summary>
    [Fact]
    public void Count_MayOmitTheValueComponent()
    {
        var rowCount = Measure(MeasureKinds.Aggregate) with { AggregationFunction = AggregationFunctions.Count, Components = [] };
        var distinctCount = rowCount with { AggregationFunction = AggregationFunctions.CountDistinct };

        _validator.TestValidate(rowCount).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(distinctCount).ShouldHaveValidationErrorFor(measure => measure.Components).WithErrorMessage("'aggregate' measures need a 'value' component.");
    }

    /// <summary>Weighted measures need a value and a weight component and take no aggregation function.</summary>
    /// <param name="kind">The weighted kind.</param>
    [Theory]
    [InlineData(MeasureKinds.WeightedSum)]
    [InlineData(MeasureKinds.WeightedAverage)]
    public void WeightedMeasures_RequireValueAndWeightAndNoFunction(string kind)
    {
        var input = Measure(kind) with { AggregationFunction = AggregationFunctions.Sum, Components = [Component(ComponentRoles.Value, 1)] };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(measure => measure.AggregationFunction);
        result.ShouldHaveValidationErrorFor(measure => measure.Components).WithErrorMessage($"'{kind}' measures need a 'weight' component.");
    }

    /// <summary>A period average needs a value component and a time policy.</summary>
    [Fact]
    public void PeriodAverage_RequiresAValueComponentAndATimePolicy()
    {
        var input = Measure(MeasureKinds.PeriodAverage) with { Components = [], TimePolicy = null };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(measure => measure.Components);
        result.ShouldHaveValidationErrorFor(measure => measure.TimePolicy);
    }

    /// <summary>A kind rejects a role it does not read, such as a weight on an aggregate.</summary>
    [Fact]
    public void RolesTheKindDoesNotRead_AreRejected()
    {
        var input = Measure(MeasureKinds.Aggregate) with { Components = [Component(ComponentRoles.Value, 1), Component(ComponentRoles.Weight, 2)] };

        _validator.TestValidate(input).ShouldHaveValidationErrorFor(measure => measure.Components).WithErrorMessage("'aggregate' measures take no 'weight' component.");
    }

    /// <summary>A measure reads one field per role, roles are vocabulary values, and each component names its field.</summary>
    [Fact]
    public void Components_HaveUniqueVocabularyRolesAndAField()
    {
        var repeated = Measure(MeasureKinds.Aggregate) with { Components = [Component(ComponentRoles.Value, 1), Component(ComponentRoles.Value, 2)] };
        var unknown = Measure(MeasureKinds.Specialized) with { Components = [Component("multiplier", 1)] };
        var withoutField = Measure(MeasureKinds.Aggregate) with { Components = [Component(ComponentRoles.Value, null)] };

        _validator.TestValidate(repeated).ShouldHaveValidationErrorFor(measure => measure.Components);
        Assert.True(HasError(_validator.TestValidate(unknown), "Components[0].Role"));
        Assert.True(HasError(_validator.TestValidate(withoutField), "Components[0].FieldId"));
    }

    /// <summary>A formula combines measures only: it parses, reads no fields or request parameters, and has no components.</summary>
    [Fact]
    public void Formula_ReadsMeasuresOnlyAndHasNoComponents()
    {
        var readingFields = Measure(MeasureKinds.Formula) with { ExpressionText = "[m.interest] / [f.balance]", Components = [Component(ComponentRoles.Value, 1)] };
        var readingParameters = Measure(MeasureKinds.Formula) with { ExpressionText = "[m.interest] * @scale" };
        var unparsable = Measure(MeasureKinds.Formula) with { ExpressionText = "[m.interest] /" };
        var empty = Measure(MeasureKinds.Formula) with { ExpressionText = null };

        var result = _validator.TestValidate(readingFields);
        result.ShouldHaveValidationErrorFor(measure => measure.ExpressionText);
        result.ShouldHaveValidationErrorFor(measure => measure.Components).WithErrorMessage("'formula' measures take no 'value' component.");
        _validator.TestValidate(readingParameters).ShouldHaveValidationErrorFor(measure => measure.ExpressionText);
        _validator.TestValidate(unparsable).ShouldHaveValidationErrorFor(measure => measure.ExpressionText).WithErrorMessage(ExpressionError("[m.interest] /"));
        _validator.TestValidate(empty).ShouldHaveValidationErrorFor(measure => measure.ExpressionText);
    }

    /// <summary>
    /// Dataset expressions stay on expression language 1.0, so a measure formula or row filter that calls POWER fails, though the
    /// shared parser reads it: only model formulas on formula language 1.1 may call it.
    /// </summary>
    [Fact]
    public void DatasetExpressions_CannotCallPower()
    {
        var formula = Measure(MeasureKinds.Formula) with { ExpressionText = "POWER([m.rate] + 1, 12) - 1" };
        var rowFilter = Measure(MeasureKinds.Aggregate) with { RowFilterExpression = "POWER([f.balance], 2) > 4" };

        _validator.TestValidate(formula).ShouldHaveValidationErrorFor(measure => measure.ExpressionText)
            .WithErrorMessage("Metadata expressions are expression language 1.0, which has no POWER, so 'Expression Text' cannot call it.");
        _validator.TestValidate(rowFilter).ShouldHaveValidationErrorFor(measure => measure.RowFilterExpression);
    }

    /// <summary>
    /// YEARFRAC reads the period a report evaluates a model formula for, which a query has none of, so a dataset expression that calls
    /// it fails, though the shared parser reads it.
    /// </summary>
    [Fact]
    public void DatasetExpressions_CannotCallYearFrac()
    {
        var formula = Measure(MeasureKinds.Formula) with { ExpressionText = "[m.interest] / YEARFRAC('actual_360')" };

        _validator.TestValidate(formula).ShouldHaveValidationErrorFor(measure => measure.ExpressionText)
            .WithErrorMessage("Metadata expressions are expression language 1.0, which has no YEARFRAC, so 'Expression Text' cannot call it.");
    }

    /// <summary>Only formulas have an expression.</summary>
    [Fact]
    public void ExpressionOnAnotherKind_IsRejected()
    {
        var input = Measure(MeasureKinds.Aggregate) with { ExpressionText = "[m.interest]" };

        _validator.TestValidate(input).ShouldHaveValidationErrorFor(measure => measure.ExpressionText);
    }

    /// <summary>A row filter must parse and read fields only, never measures or request parameters.</summary>
    /// <param name="filter">The filter sent.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("[f.status] = 'open'", true)]
    [InlineData("[f.balance] > [m.averageBalance]", false)]
    [InlineData("[f.status] = @status", false)]
    [InlineData("[f.status] =", false)]
    [InlineData("", false)]
    public void RowFilter_MustParseAndReadFieldsOnly(string filter, bool valid)
    {
        var result = _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { RowFilterExpression = filter });

        Assert.Equal(!valid, HasError(result, nameof(DatasetMeasureInput.RowFilterExpression)));
    }

    /// <summary>A specialized measure names its implementation and version, and its configuration must be JSON.</summary>
    [Fact]
    public void Specialized_RequiresAnImplementationAndJsonConfiguration()
    {
        var input = Measure(MeasureKinds.Specialized) with { ImplementationKey = null, ImplementationVersion = "", ConfigurationJson = "{not json" };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(measure => measure.ImplementationKey);
        result.ShouldHaveValidationErrorFor(measure => measure.ImplementationVersion);
        result.ShouldHaveValidationErrorFor(measure => measure.ConfigurationJson);
    }

    /// <summary>Only specialized measures name an implementation.</summary>
    [Fact]
    public void ImplementationOnAnotherKind_IsRejected()
    {
        var input = Measure(MeasureKinds.Aggregate) with { ImplementationKey = "irr", ImplementationVersion = "1", ConfigurationJson = "{}" };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(measure => measure.ImplementationKey);
        result.ShouldHaveValidationErrorFor(measure => measure.ImplementationVersion);
        result.ShouldHaveValidationErrorFor(measure => measure.ConfigurationJson);
    }

    /// <summary>
    /// A decimal output needs a precision of 1-38 and a scale from 0 up to the precision; other outputs carry neither. A
    /// missing precision is reported on the precision alone, not also as a scale that exceeds it.
    /// </summary>
    /// <param name="dataType">The output data type sent.</param>
    /// <param name="precision">The output precision sent.</param>
    /// <param name="scale">The output scale sent.</param>
    /// <param name="precisionFails">Whether the precision is rejected.</param>
    /// <param name="scaleFails">Whether the scale is rejected.</param>
    [Theory]
    [InlineData(MeasureOutputDataTypes.Decimal, 20, 2, false, false)]
    [InlineData(MeasureOutputDataTypes.Decimal, 38, 38, false, false)]
    [InlineData(MeasureOutputDataTypes.Decimal, null, null, true, true)]
    [InlineData(MeasureOutputDataTypes.Decimal, null, 2, true, false)]
    [InlineData(MeasureOutputDataTypes.Decimal, 39, 2, true, false)]
    [InlineData(MeasureOutputDataTypes.Decimal, 10, 11, false, true)]
    [InlineData(MeasureOutputDataTypes.Int64, null, null, false, false)]
    [InlineData(MeasureOutputDataTypes.Int64, 18, 0, true, true)]
    public void DecimalOutput_RequiresPrecisionAndScaleInRange(string dataType, int? precision, int? scale, bool precisionFails, bool scaleFails)
    {
        var input = Measure(MeasureKinds.Aggregate) with { OutputDataType = dataType, OutputPrecision = precision, OutputScale = scale };

        var result = _validator.TestValidate(input);

        Assert.Equal(precisionFails, HasError(result, nameof(DatasetMeasureInput.OutputPrecision)));
        Assert.Equal(scaleFails, HasError(result, nameof(DatasetMeasureInput.OutputScale)));
    }

    /// <summary>The accumulator is a StarRocks decimal: a precision of 1-38 and a scale from 0 up to it.</summary>
    [Fact]
    public void Accumulator_MustBeAStarRocksDecimal()
    {
        var outOfRange = _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { AccumulatorPrecision = 39, AccumulatorScale = 40 });
        var scaleAbovePrecision = _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { AccumulatorPrecision = 18, AccumulatorScale = 20 });

        outOfRange.ShouldHaveValidationErrorFor(measure => measure.AccumulatorPrecision);
        outOfRange.ShouldHaveValidationErrorFor(measure => measure.AccumulatorScale);
        scaleAbovePrecision.ShouldNotHaveValidationErrorFor(measure => measure.AccumulatorPrecision);
        scaleAbovePrecision.ShouldHaveValidationErrorFor(measure => measure.AccumulatorScale);
    }

    /// <summary>
    /// Matching the latest observation at or before a boundary searches back in time, so the lookback must be a positive
    /// number of seconds (schema section 18); exact matching searches nothing and needs none.
    /// </summary>
    /// <param name="boundaryMatch">The boundary match sent.</param>
    /// <param name="lookbackSeconds">The lookback sent.</param>
    /// <param name="valid">Whether the policy is accepted.</param>
    [Theory]
    [InlineData(BoundaryMatches.OnOrBefore, 86400L, true)]
    [InlineData(BoundaryMatches.StrictlyBefore, 3600L, true)]
    [InlineData(BoundaryMatches.Exact, null, true)]
    [InlineData(BoundaryMatches.OnOrBefore, null, false)]
    [InlineData(BoundaryMatches.StrictlyBefore, null, false)]
    [InlineData(BoundaryMatches.OnOrBefore, 0L, false)]
    [InlineData(BoundaryMatches.Exact, -1L, false)]
    public void PriorBoundaryMatches_RequireAFiniteLookback(string boundaryMatch, long? lookbackSeconds, bool valid)
    {
        var policy = TimePolicy() with { BoundaryMatch = boundaryMatch, MaxLookbackSeconds = lookbackSeconds };

        var result = _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { TimePolicy = policy });

        Assert.Equal(!valid, HasError(result, "TimePolicy.MaxLookbackSeconds"));
    }

    /// <summary>Selecting single observations happens per series, so it needs the series grain to select by.</summary>
    [Fact]
    public void SelectingObservations_RequiresASelectionGrain()
    {
        var policy = TimePolicy() with { ObservationSelection = ObservationSelections.AtEnd, SelectionDatasetGrainId = null };

        _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { TimePolicy = policy })
            .ShouldHaveValidationErrorFor(measure => measure.TimePolicy!.SelectionDatasetGrainId);
    }

    /// <summary>A measure key starts with a letter and holds only letters, digits and underscores.</summary>
    /// <param name="measureKey">The key sent.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("endingBalance", true)]
    [InlineData("rate_30d", true)]
    [InlineData("30dRate", false)]
    [InlineData("ending-balance", false)]
    public void MeasureKey_MustMatchThePattern(string measureKey, bool valid)
    {
        var result = _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { Key = measureKey });

        Assert.Equal(!valid, HasError(result, nameof(DatasetMeasureInput.Key)));
    }

    /// <summary>A valid measure of a kind with the schema's default behaviors and a decimal(20, 2) output.</summary>
    private static DatasetMeasureInput Measure(string kind)
    {
        var measure = new DatasetMeasureInput
        {
            Key = "endingBalance",
            DisplayName = "Ending balance",
            MeasureKind = kind,
            AggregationFunction = null,
            ExpressionText = null,
            ImplementationKey = null,
            ImplementationVersion = null,
            ConfigurationJson = null,
            RowFilterExpression = null,
            RoundingMode = null,
            UnitKey = null,
            TimePolicy = null,
            ValueNullBehavior = ValueNullBehaviors.Exclude,
            NegativeWeightBehavior = NegativeWeightBehaviors.Allow,
            EmptySetBehavior = EmptySetBehaviors.Null,
            BridgeFactorBehavior = BridgeFactorBehaviors.Ignore,
            BridgeAllocationBehavior = BridgeAllocationBehaviors.Apply,
            TotalBehavior = TotalBehaviors.Recompute,
            OutputDataType = MeasureOutputDataTypes.Decimal,
            OutputPrecision = 20,
            OutputScale = 2,
            AccumulatorPrecision = 38,
            AccumulatorScale = 12,
            IsEnabled = true,
            Components = [Component(ComponentRoles.Value, 1)]
        };
        return kind switch
        {
            MeasureKinds.Aggregate => measure with { AggregationFunction = AggregationFunctions.Sum },
            MeasureKinds.WeightedSum or MeasureKinds.WeightedAverage => measure with { Components = [Component(ComponentRoles.Value, 1), Component(ComponentRoles.Weight, 2)] },
            MeasureKinds.PeriodAverage => measure with { TimePolicy = TimePolicy() },
            MeasureKinds.Formula => measure with { ExpressionText = "[m.interest] / [m.balance]", Components = [] },
            _ => measure with { ImplementationKey = "irr", ImplementationVersion = "1.0", ConfigurationJson = """{"periods":12}""" }
        };
    }

    /// <summary>A component of a role.</summary>
    private static DatasetMeasureComponent Component(string role, long? fieldId)
    {
        return new DatasetMeasureComponent { Role = role, FieldId = fieldId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty };
    }

    /// <summary>A time policy that sums every observation by calendar day.</summary>
    private static DatasetMeasureTimePolicy TimePolicy()
    {
        return new DatasetMeasureTimePolicy
        {
            RequiredTimeBindingId = null,
            SelectionDatasetGrainId = null,
            MaxLookbackSeconds = null,
            ObservationSelection = ObservationSelections.All,
            ValueTimeRole = ValueTimeRoles.Point,
            BoundaryMatch = BoundaryMatches.Exact,
            DurationUnit = DurationUnits.CalendarDay,
            PartialIntervalBehavior = PartialIntervalBehaviors.ExactOnly,
            MissingPeriodBehavior = MissingPeriodBehaviors.Null
        };
    }

    /// <summary>The query engine's parse error for an expression.</summary>
    private static string ExpressionError(string text)
    {
        return ExpressionInspector.Inspect(text).Error!;
    }

    /// <summary>Whether the result has an error for a property path, however the validator names properties.</summary>
    private static bool HasError(TestValidationResult<DatasetMeasureInput> result, string path)
    {
        return result.Errors.Any(error => string.Equals(error.PropertyName, path, StringComparison.OrdinalIgnoreCase));
    }
}
