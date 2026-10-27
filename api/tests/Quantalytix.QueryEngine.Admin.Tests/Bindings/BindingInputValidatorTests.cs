using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms the binding input shape of schema sections 11 and 12: a breakout binding names a role and a time binding its dimension,
/// each binding kind requires its own references and forbids the others', the unknown unmapped behavior needs its member, and a time
/// binding's axis carries valid periodicities, IANA time zones the query engine accepts, a source zone exactly for timestamps and a
/// fixed start exactly for that origin. The engine resolves bindings from exactly these columns, so a binding that breaks them
/// misroutes or fails at query time. Whether the role exists and whether a required binding stays enabled is decided as the
/// operation applies, against the version.
/// </summary>
public sealed class BindingInputValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly DatasetBindingInputValidator _validator = new();

    /// <summary>A valid binding of each kind passes.</summary>
    /// <param name="kind">The binding's kind.</param>
    [Theory]
    [InlineData(BindingKinds.Direct)]
    [InlineData(BindingKinds.Mapped)]
    [InlineData(BindingKinds.Range)]
    [InlineData(BindingKinds.Bridge)]
    [InlineData(BindingKinds.Time)]
    public void ValidBindingOfEachKind_Passes(string kind)
    {
        _validator.TestValidate(Binding(kind)).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A breakout binding names its role and no dimension of its own: it classifies into the role's dimension.</summary>
    [Fact]
    public void BreakoutBinding_NamesARoleAndNoDimension()
    {
        _validator.TestValidate(Binding(BindingKinds.Direct) with { DimensionRoleId = null }).ShouldHaveValidationErrorFor(binding => binding.DimensionRoleId);
        _validator.TestValidate(Binding(BindingKinds.Direct) with { DimensionId = "7000" }).ShouldHaveValidationErrorFor(binding => binding.DimensionId);
    }

    /// <summary>A time binding names its time dimension and no role, because time dimensions have no roles.</summary>
    [Fact]
    public void TimeBinding_NamesItsDimensionAndNoRole()
    {
        _validator.TestValidate(Binding(BindingKinds.Time) with { DimensionId = null }).ShouldHaveValidationErrorFor(binding => binding.DimensionId);
        _validator.TestValidate(Binding(BindingKinds.Time) with { DimensionRoleId = "81" }).ShouldHaveValidationErrorFor(binding => binding.DimensionRoleId);
    }

    /// <summary>Every kind but bridge classifies a source field's value, so it needs the field; a bridge binding may omit it.</summary>
    /// <param name="kind">The binding's kind.</param>
    /// <param name="required">Whether the source field is required.</param>
    [Theory]
    [InlineData(BindingKinds.Direct, true)]
    [InlineData(BindingKinds.Mapped, true)]
    [InlineData(BindingKinds.Range, true)]
    [InlineData(BindingKinds.Time, true)]
    [InlineData(BindingKinds.Bridge, false)]
    public void SourceField_IsRequiredExceptForBridges(string kind, bool required)
    {
        var result = _validator.TestValidate(Binding(kind) with { SourceFieldId = null });

        Assert.Equal(required, HasError(result, nameof(DatasetBindingInput.SourceFieldId)));
    }

    /// <summary>A mapped binding needs its mapping set, and no other kind may name one.</summary>
    [Fact]
    public void MappingSet_IsRequiredForMappedAndForbiddenOtherwise()
    {
        _validator.TestValidate(Binding(BindingKinds.Mapped) with { MappingSetId = null }).ShouldHaveValidationErrorFor(binding => binding.MappingSetId);
        _validator.TestValidate(Binding(BindingKinds.Direct) with { MappingSetId = "30" }).ShouldHaveValidationErrorFor(binding => binding.MappingSetId);
    }

    /// <summary>A bridge binding needs its source binding and bridge, and no other kind may name either.</summary>
    [Fact]
    public void BridgeReferences_AreRequiredForBridgeAndForbiddenOtherwise()
    {
        var missing = _validator.TestValidate(Binding(BindingKinds.Bridge) with { SourceDimensionBindingId = null, BridgeId = null });
        var foreign = _validator.TestValidate(Binding(BindingKinds.Mapped) with { SourceDimensionBindingId = "21", BridgeId = "40" });

        missing.ShouldHaveValidationErrorFor(binding => binding.SourceDimensionBindingId);
        missing.ShouldHaveValidationErrorFor(binding => binding.BridgeId);
        foreign.ShouldHaveValidationErrorFor(binding => binding.SourceDimensionBindingId);
        foreign.ShouldHaveValidationErrorFor(binding => binding.BridgeId);
    }

    /// <summary>A time binding needs its time axis, and no other kind may send one.</summary>
    [Fact]
    public void TimeAxis_IsRequiredForTimeAndForbiddenOtherwise()
    {
        _validator.TestValidate(Binding(BindingKinds.Time) with { Time = null }).ShouldHaveValidationErrorFor(binding => binding.Time);
        _validator.TestValidate(Binding(BindingKinds.Direct) with { Time = Time() }).ShouldHaveValidationErrorFor(binding => binding.Time);
    }

    /// <summary>Routing unmapped rows to the unknown member needs that member; other unmapped behaviors name none.</summary>
    [Fact]
    public void UnknownMember_IsRequiredExactlyForTheUnknownBehavior()
    {
        var missing = Binding(BindingKinds.Mapped) with { UnmappedBehavior = UnmappedBehaviors.Unknown, UnknownMemberKey = null };
        var unused = Binding(BindingKinds.Mapped) with { UnmappedBehavior = UnmappedBehaviors.Exclude, UnknownMemberKey = "UNKNOWN" };

        _validator.TestValidate(missing).ShouldHaveValidationErrorFor(binding => binding.UnknownMemberKey);
        _validator.TestValidate(unused).ShouldHaveValidationErrorFor(binding => binding.UnknownMemberKey);
        _validator.TestValidate(missing with { UnknownMemberKey = "UNKNOWN" }).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Ids are stored or temporary ids, which the change set resolves.</summary>
    [Fact]
    public void Ids_AreStoredOrTemporary()
    {
        _validator.TestValidate(Binding(BindingKinds.Direct) with { SourceFieldId = "field-1" }).ShouldHaveValidationErrorFor(binding => binding.SourceFieldId);
        _validator.TestValidate(Binding(BindingKinds.Direct) with { SourceFieldId = "tmp:balance" }).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Kinds and behaviors must be vocabulary values.</summary>
    [Fact]
    public void UnknownVocabularyValues_AreRejected()
    {
        var input = Binding(BindingKinds.Direct) with { BindingKind = "lookup", RaggedBehavior = "skip", DefaultPeriodAlignment = "middle", UnmappedBehavior = "drop" };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(binding => binding.BindingKind);
        result.ShouldHaveValidationErrorFor(binding => binding.RaggedBehavior);
        result.ShouldHaveValidationErrorFor(binding => binding.DefaultPeriodAlignment);
        result.ShouldHaveValidationErrorFor(binding => binding.UnmappedBehavior);
    }

    /// <summary>The allowed periodicities are a non-empty set of distinct periodicity values.</summary>
    /// <param name="periodicities">The periodicities sent, comma-separated.</param>
    /// <param name="valid">Whether they are accepted.</param>
    [Theory]
    [InlineData("day,month,quarter,year", true)]
    [InlineData("month", true)]
    [InlineData("", false)]
    [InlineData("day,day", false)]
    [InlineData("day,week", false)]
    public void AllowedPeriodicities_AreADistinctNonEmptySubset(string periodicities, bool valid)
    {
        var allowed = periodicities.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = _validator.TestValidate(Binding(BindingKinds.Time) with { Time = Time() with { AllowedPeriodicities = allowed } });

        Assert.Equal(!valid, result.Errors.Any(error => error.PropertyName.StartsWith("Time.AllowedPeriodicities", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Timestamps are recorded in a source time zone, which dates do not have.</summary>
    [Fact]
    public void SourceTimeZone_IsRequiredForTimestampsAndForbiddenForDates()
    {
        var timestamp = Binding(BindingKinds.Time) with { Time = Time() with { TimeValueType = TimeValueTypes.Timestamp, SourceTimeZone = null } };
        var date = Binding(BindingKinds.Time) with { Time = Time() with { SourceTimeZone = "America/Chicago" } };

        _validator.TestValidate(timestamp).ShouldHaveValidationErrorFor(binding => binding.Time!.SourceTimeZone);
        _validator.TestValidate(date).ShouldHaveValidationErrorFor(binding => binding.Time!.SourceTimeZone);
    }

    /// <summary>
    /// Time zones must be IANA zones, the query engine's own test: a Windows zone name resolves on most systems but has no IANA name,
    /// so the engine would reject it.
    /// </summary>
    /// <param name="timeZone">The zone sent as both the source and the default query zone.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("UTC", true)]
    [InlineData("America/Chicago", true)]
    [InlineData("Europe/London", true)]
    [InlineData("Central Standard Time", false)]
    [InlineData("Mars/Olympus_Mons", false)]
    public void TimeZones_MustBeIanaZones(string timeZone, bool valid)
    {
        var axis = Time() with { TimeValueType = TimeValueTypes.Timestamp, SourceTimeZone = timeZone, DefaultQueryTimeZone = timeZone };

        var result = _validator.TestValidate(Binding(BindingKinds.Time) with { Time = axis });

        Assert.Equal(!valid, HasError(result, "Time.SourceTimeZone"));
        Assert.Equal(!valid, HasError(result, "Time.DefaultQueryTimeZone"));
    }

    /// <summary>The fixed start is required exactly for the fixed_start cumulative origin.</summary>
    [Fact]
    public void FixedStart_IsRequiredExactlyForTheFixedStartOrigin()
    {
        var missing = Time() with { CumulativeOrigin = CumulativeOrigins.FixedStart, CumulativeFixedStart = null };
        var unused = Time() with { CumulativeFixedStart = new DateOnly(2026, 1, 1) };

        _validator.TestValidate(Binding(BindingKinds.Time) with { Time = missing }).ShouldHaveValidationErrorFor(binding => binding.Time!.CumulativeFixedStart);
        _validator.TestValidate(Binding(BindingKinds.Time) with { Time = unused }).ShouldHaveValidationErrorFor(binding => binding.Time!.CumulativeFixedStart);
        _validator.TestValidate(Binding(BindingKinds.Time) with { Time = missing with { CumulativeFixedStart = new DateOnly(2026, 1, 1) } }).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A valid binding of a kind with the schema's default behaviors: of role 81, or of time dimension 7000 for <c>time</c>.</summary>
    private static DatasetBindingInput Binding(string kind)
    {
        var binding = new DatasetBindingInput
        {
            BindingKind = kind,
            DimensionRoleId = "81",
            DimensionId = null,
            SourceFieldId = "1",
            MappingSetId = null,
            BridgeId = null,
            SourceDimensionBindingId = null,
            RaggedBehavior = RaggedBehaviors.Null,
            DefaultPeriodAlignment = PeriodAlignments.Observation,
            UnmappedBehavior = UnmappedBehaviors.Null,
            UnknownMemberKey = null,
            IsRequired = false,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true,
            Time = null
        };
        return kind switch
        {
            BindingKinds.Mapped => binding with { MappingSetId = "30" },
            BindingKinds.Bridge => binding with { SourceFieldId = null, SourceDimensionBindingId = "21", BridgeId = "40" },
            BindingKinds.Time => binding with { DimensionRoleId = null, DimensionId = "7000", Time = Time() },
            _ => binding
        };
    }

    /// <summary>A valid time axis: daily snapshots of a date, grouped by any periodicity, queried in UTC.</summary>
    private static DatasetTimeBinding Time()
    {
        return new DatasetTimeBinding
        {
            TimeValueType = TimeValueTypes.Date,
            SourceTimeZone = null,
            DefaultQueryTimeZone = "UTC",
            ObservationKind = ObservationKinds.Snapshot,
            NativePeriodicity = Periodicities.Day,
            IntervalEndFieldId = null,
            AllowedPeriodicities = [Periodicities.Day, Periodicities.Month, Periodicities.Quarter, Periodicities.Year],
            CumulativeOrigin = CumulativeOrigins.OutputRangeStart,
            CumulativeFixedStart = null
        };
    }

    /// <summary>Whether the result has an error for a property path, however the validator names properties.</summary>
    private static bool HasError(TestValidationResult<DatasetBindingInput> result, string path)
    {
        return result.Errors.Any(error => string.Equals(error.PropertyName, path, StringComparison.OrdinalIgnoreCase));
    }
}
