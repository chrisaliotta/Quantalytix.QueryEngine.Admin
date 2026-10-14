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
    private readonly MeasureInputValidator _validator = new();

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

    /// <summary>A measure reads one field per role, and roles are vocabulary values.</summary>
    [Fact]
    public void ComponentRoles_AreUniqueVocabularyValues()
    {
        var repeated = Measure(MeasureKinds.Aggregate) with { Components = [Component(ComponentRoles.Value, 1), Component(ComponentRoles.Value, 2)] };
        var unknown = Measure(MeasureKinds.Specialized) with { Components = [Component("multiplier", 1)] };

        _validator.TestValidate(repeated).ShouldHaveValidationErrorFor(measure => measure.Components);
        Assert.True(HasError(_validator.TestValidate(unknown), "Components[0].ComponentRole"));
    }

    /// <summary>A formula combines measures only: it parses, reads no fields, and has no components.</summary>
    [Fact]
    public void Formula_ReadsMeasuresOnlyAndHasNoComponents()
    {
        var readingFields = Measure(MeasureKinds.Formula) with { ExpressionText = "[m.interest] / [f.balance]", Components = [Component(ComponentRoles.Value, 1)] };
        var unparsable = Measure(MeasureKinds.Formula) with { ExpressionText = "[m.interest] /" };
        var empty = Measure(MeasureKinds.Formula) with { ExpressionText = null };

        var result = _validator.TestValidate(readingFields);
        result.ShouldHaveValidationErrorFor(measure => measure.ExpressionText);
        result.ShouldHaveValidationErrorFor(measure => measure.Components).WithErrorMessage("'formula' measures take no 'value' component.");
        _validator.TestValidate(unparsable).ShouldHaveValidationErrorFor(measure => measure.ExpressionText).WithErrorMessage(ExpressionError("[m.interest] /"));
        _validator.TestValidate(empty).ShouldHaveValidationErrorFor(measure => measure.ExpressionText);
    }

    /// <summary>Only formulas have an expression.</summary>
    [Fact]
    public void ExpressionOnAnotherKind_IsRejected()
    {
        var input = Measure(MeasureKinds.Aggregate) with { ExpressionText = "[m.interest]" };

        _validator.TestValidate(input).ShouldHaveValidationErrorFor(measure => measure.ExpressionText);
    }

    /// <summary>A row filter must parse and read fields only.</summary>
    /// <param name="filter">The filter sent.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("[f.status] = 'open'", true)]
    [InlineData("[f.balance] > [m.averageBalance]", false)]
    [InlineData("[f.status] =", false)]
    [InlineData("", false)]
    public void RowFilter_MustParseAndReadFieldsOnly(string filter, bool valid)
    {
        var result = _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { RowFilterExpression = filter });

        Assert.Equal(!valid, HasError(result, nameof(MeasureInput.RowFilterExpression)));
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

    /// <summary>A decimal output needs a precision of 1-38 and a scale from 0 up to the precision; other outputs carry neither.</summary>
    /// <param name="dataType">The output data type sent.</param>
    /// <param name="precision">The output precision sent.</param>
    /// <param name="scale">The output scale sent.</param>
    /// <param name="precisionFails">Whether the precision is rejected.</param>
    /// <param name="scaleFails">Whether the scale is rejected.</param>
    [Theory]
    [InlineData(MeasureOutputDataTypes.Decimal, 20, 2, false, false)]
    [InlineData(MeasureOutputDataTypes.Decimal, 38, 38, false, false)]
    [InlineData(MeasureOutputDataTypes.Decimal, null, null, true, true)]
    [InlineData(MeasureOutputDataTypes.Decimal, 39, 2, true, false)]
    [InlineData(MeasureOutputDataTypes.Decimal, 10, 11, false, true)]
    [InlineData(MeasureOutputDataTypes.Int64, null, null, false, false)]
    [InlineData(MeasureOutputDataTypes.Int64, 18, 0, true, true)]
    public void DecimalOutput_RequiresPrecisionAndScaleInRange(string dataType, int? precision, int? scale, bool precisionFails, bool scaleFails)
    {
        var input = Measure(MeasureKinds.Aggregate) with { OutputDataType = dataType, OutputPrecision = precision, OutputScale = scale };

        var result = _validator.TestValidate(input);

        Assert.Equal(precisionFails, HasError(result, nameof(MeasureInput.OutputPrecision)));
        Assert.Equal(scaleFails, HasError(result, nameof(MeasureInput.OutputScale)));
    }

    /// <summary>The accumulator is a StarRocks decimal: a precision of 1-38 and a scale from 0 up to it.</summary>
    [Fact]
    public void Accumulator_MustBeAStarRocksDecimal()
    {
        var result = _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { AccumulatorPrecision = 39, AccumulatorScale = 40 });

        result.ShouldHaveValidationErrorFor(measure => measure.AccumulatorPrecision);
        result.ShouldHaveValidationErrorFor(measure => measure.AccumulatorScale);
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
        var policy = TimePolicy() with { ObservationSelection = ObservationSelections.AtEnd, SelectionGrainId = null };

        _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { TimePolicy = policy })
            .ShouldHaveValidationErrorFor(measure => measure.TimePolicy!.SelectionGrainId);
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
        var result = _validator.TestValidate(Measure(MeasureKinds.Aggregate) with { MeasureKey = measureKey });

        Assert.Equal(!valid, HasError(result, nameof(MeasureInput.MeasureKey)));
    }

    /// <summary>A valid measure of a kind with the schema's default behaviors and a decimal(20, 2) output.</summary>
    private static MeasureInput Measure(string kind)
    {
        var measure = new MeasureInput
        {
            MeasureKey = "endingBalance",
            DisplayName = "Ending balance",
            MeasureKind = kind,
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
    private static MeasureComponentInput Component(string role, long fieldId)
    {
        return new MeasureComponentInput { ComponentRole = role, FieldId = fieldId };
    }

    /// <summary>A time policy that sums every observation by calendar day.</summary>
    private static MeasureTimePolicyInput TimePolicy()
    {
        return new MeasureTimePolicyInput
        {
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
    private static bool HasError(TestValidationResult<MeasureInput> result, string path)
    {
        return result.Errors.Any(error => string.Equals(error.PropertyName, path, StringComparison.OrdinalIgnoreCase));
    }
}
