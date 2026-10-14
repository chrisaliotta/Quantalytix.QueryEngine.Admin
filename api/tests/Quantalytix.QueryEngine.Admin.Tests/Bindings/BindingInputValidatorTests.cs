using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms the binding input rules of schema sections 11 and 12: each binding kind requires its own references and
/// forbids the others', each grouping mode requires exactly its own grouping reference, the unknown unmapped behavior needs
/// its member, a required binding stays enabled, and a time binding's extension carries valid periodicities, IANA time
/// zones the query engine accepts, a source zone exactly for timestamps and a fixed start exactly for that origin. The
/// engine resolves bindings from exactly these columns, so a binding that breaks them misroutes or fails at query time.
/// </summary>
public sealed class BindingInputValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly BindingInputValidator _validator = new();

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

    /// <summary>A binding classifies into a dimension, so one must be chosen; the error lands on the dimension, not in a binding failure.</summary>
    [Fact]
    public void Dimension_IsRequired()
    {
        _validator.TestValidate(Binding(BindingKinds.Direct) with { DimensionId = null }).ShouldHaveValidationErrorFor(binding => binding.DimensionId);
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

        Assert.Equal(required, HasError(result, nameof(BindingInput.SourceFieldId)));
    }

    /// <summary>A mapped binding needs its mapping set, and no other kind may name one.</summary>
    [Fact]
    public void MappingSet_IsRequiredForMappedAndForbiddenOtherwise()
    {
        _validator.TestValidate(Binding(BindingKinds.Mapped) with { MappingSetId = null }).ShouldHaveValidationErrorFor(binding => binding.MappingSetId);
        _validator.TestValidate(Binding(BindingKinds.Direct) with { MappingSetId = 30 }).ShouldHaveValidationErrorFor(binding => binding.MappingSetId);
    }

    /// <summary>A bridge binding needs its source binding and bridge version, and no other kind may name either.</summary>
    [Fact]
    public void BridgeReferences_AreRequiredForBridgeAndForbiddenOtherwise()
    {
        var missing = _validator.TestValidate(Binding(BindingKinds.Bridge) with { SourceBindingId = null, BridgeVersionId = null });
        var foreign = _validator.TestValidate(Binding(BindingKinds.Mapped) with { SourceBindingId = 21, BridgeVersionId = 40 });

        missing.ShouldHaveValidationErrorFor(binding => binding.SourceBindingId);
        missing.ShouldHaveValidationErrorFor(binding => binding.BridgeVersionId);
        foreign.ShouldHaveValidationErrorFor(binding => binding.SourceBindingId);
        foreign.ShouldHaveValidationErrorFor(binding => binding.BridgeVersionId);
    }

    /// <summary>A time binding needs its time extension, and no other kind may send one.</summary>
    [Fact]
    public void TimeExtension_IsRequiredForTimeAndForbiddenOtherwise()
    {
        _validator.TestValidate(Binding(BindingKinds.Time) with { TimeBinding = null }).ShouldHaveValidationErrorFor(binding => binding.TimeBinding);
        _validator.TestValidate(Binding(BindingKinds.Direct) with { TimeBinding = TimeBinding() }).ShouldHaveValidationErrorFor(binding => binding.TimeBinding);
    }

    /// <summary>Each grouping mode needs exactly its own reference: a level, an ancestor member, or a depth of 0 or more.</summary>
    /// <param name="mode">The grouping mode sent.</param>
    /// <param name="levelId">The grouping level sent.</param>
    /// <param name="ancestorId">The grouping ancestor member sent.</param>
    /// <param name="depth">The grouping depth sent.</param>
    /// <param name="failing">The property expected to fail, or null when the binding is valid.</param>
    [Theory]
    [InlineData(GroupingModes.Member, null, null, null, null)]
    [InlineData(GroupingModes.Level, 501L, null, null, null)]
    [InlineData(GroupingModes.Ancestor, null, 601L, null, null)]
    [InlineData(GroupingModes.Depth, null, null, 0, null)]
    [InlineData(GroupingModes.Level, null, null, null, nameof(BindingInput.GroupingLevelId))]
    [InlineData(GroupingModes.Ancestor, null, null, null, nameof(BindingInput.GroupingAncestorMemberId))]
    [InlineData(GroupingModes.Depth, null, null, null, nameof(BindingInput.GroupingDepth))]
    [InlineData(GroupingModes.Depth, null, null, -1, nameof(BindingInput.GroupingDepth))]
    [InlineData(GroupingModes.Member, 501L, null, null, nameof(BindingInput.GroupingLevelId))]
    [InlineData(GroupingModes.Level, 501L, 601L, null, nameof(BindingInput.GroupingAncestorMemberId))]
    [InlineData(GroupingModes.Ancestor, null, 601L, 2, nameof(BindingInput.GroupingDepth))]
    public void GroupingMode_RequiresExactlyItsReference(string mode, long? levelId, long? ancestorId, int? depth, string? failing)
    {
        var input = Binding(BindingKinds.Direct) with
        {
            GroupingMode = mode,
            GroupingLevelId = levelId,
            GroupingAncestorMemberId = ancestorId,
            GroupingDepth = depth
        };

        var result = _validator.TestValidate(input);

        if (failing is null) result.ShouldNotHaveAnyValidationErrors();
        else Assert.True(HasError(result, failing));
    }

    /// <summary>Routing unmapped rows to the unknown member needs that member; other unmapped behaviors name none.</summary>
    [Fact]
    public void UnknownMember_IsRequiredExactlyForTheUnknownBehavior()
    {
        var missing = Binding(BindingKinds.Mapped) with { UnmappedBehavior = UnmappedBehaviors.Unknown, UnknownMemberId = null };
        var unused = Binding(BindingKinds.Mapped) with { UnmappedBehavior = UnmappedBehaviors.Exclude, UnknownMemberId = 700 };

        _validator.TestValidate(missing).ShouldHaveValidationErrorFor(binding => binding.UnknownMemberId);
        _validator.TestValidate(unused).ShouldHaveValidationErrorFor(binding => binding.UnknownMemberId);
        _validator.TestValidate(missing with { UnknownMemberId = 700 }).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A required binding is a dataset fixture that must not be disabled (schema section 11).</summary>
    [Fact]
    public void RequiredBinding_MustStayEnabled()
    {
        var input = Binding(BindingKinds.Direct) with { IsRequired = true, IsEnabled = false };

        _validator.TestValidate(input).ShouldHaveValidationErrorFor(binding => binding.IsEnabled);
    }

    /// <summary>Kinds, grouping modes and behaviors must be vocabulary values.</summary>
    [Fact]
    public void UnknownVocabularyValues_AreRejected()
    {
        var input = Binding(BindingKinds.Direct) with
        {
            BindingKind = "lookup",
            GroupingMode = "tree",
            RaggedBehavior = "skip",
            DefaultPeriodAlignment = "middle",
            UnmappedBehavior = "drop"
        };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(binding => binding.BindingKind);
        result.ShouldHaveValidationErrorFor(binding => binding.GroupingMode);
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
        var result = _validator.TestValidate(Binding(BindingKinds.Time) with { TimeBinding = TimeBinding() with { AllowedPeriodicities = allowed } });

        Assert.Equal(!valid, result.Errors.Any(error => error.PropertyName.StartsWith("TimeBinding.AllowedPeriodicities", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Timestamps are recorded in a source time zone, which dates do not have.</summary>
    [Fact]
    public void SourceTimeZone_IsRequiredForTimestampsAndForbiddenForDates()
    {
        var timestamp = Binding(BindingKinds.Time) with { TimeBinding = TimeBinding() with { TimeValueType = TimeValueTypes.Timestamp, SourceTimeZone = null } };
        var date = Binding(BindingKinds.Time) with { TimeBinding = TimeBinding() with { SourceTimeZone = "America/Chicago" } };

        _validator.TestValidate(timestamp).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.SourceTimeZone);
        _validator.TestValidate(date).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.SourceTimeZone);
    }

    /// <summary>
    /// Time zones must be IANA zones, the query engine's own test: a Windows zone name resolves on most systems but has no
    /// IANA name, so the engine would reject it.
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
        var extension = TimeBinding() with { TimeValueType = TimeValueTypes.Timestamp, SourceTimeZone = timeZone, DefaultQueryTimeZone = timeZone };

        var result = _validator.TestValidate(Binding(BindingKinds.Time) with { TimeBinding = extension });

        Assert.Equal(!valid, HasError(result, "TimeBinding.SourceTimeZone"));
        Assert.Equal(!valid, HasError(result, "TimeBinding.DefaultQueryTimeZone"));
    }

    /// <summary>The fixed start is required exactly for the fixed_start cumulative origin, and must be a UTC time.</summary>
    [Fact]
    public void FixedStart_IsRequiredExactlyForTheFixedStartOrigin()
    {
        var missing = TimeBinding() with { CumulativeOrigin = CumulativeOrigins.FixedStart, CumulativeFixedStart = null };
        var unused = TimeBinding() with { CumulativeFixedStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var local = missing with { CumulativeFixedStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local) };

        _validator.TestValidate(Binding(BindingKinds.Time) with { TimeBinding = missing }).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.CumulativeFixedStart);
        _validator.TestValidate(Binding(BindingKinds.Time) with { TimeBinding = unused }).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.CumulativeFixedStart);
        _validator.TestValidate(Binding(BindingKinds.Time) with { TimeBinding = local }).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.CumulativeFixedStart);
    }

    /// <summary>A valid binding of a kind to dimension 7000, grouping by member, with the schema's default behaviors.</summary>
    private static BindingInput Binding(string kind)
    {
        var binding = new BindingInput
        {
            DisplayName = "Branch",
            DimensionId = 7000,
            BindingKind = kind,
            SourceFieldId = 1,
            GroupingMode = GroupingModes.Member,
            RaggedBehavior = RaggedBehaviors.Null,
            DefaultPeriodAlignment = PeriodAlignments.Observation,
            UnmappedBehavior = UnmappedBehaviors.Null,
            IsRequired = false,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true
        };
        return kind switch
        {
            BindingKinds.Mapped => binding with { MappingSetId = 30 },
            BindingKinds.Bridge => binding with { SourceFieldId = null, SourceBindingId = 21, BridgeVersionId = 40 },
            BindingKinds.Time => binding with { TimeBinding = TimeBinding() },
            _ => binding
        };
    }

    /// <summary>A valid time extension: daily snapshots of a date, grouped by any periodicity, queried in UTC.</summary>
    private static TimeBindingInput TimeBinding()
    {
        return new TimeBindingInput
        {
            TimeValueType = TimeValueTypes.Date,
            ObservationKind = ObservationKinds.Snapshot,
            NativePeriodicity = Periodicities.Day,
            AllowedPeriodicities = [Periodicities.Day, Periodicities.Month, Periodicities.Quarter, Periodicities.Year],
            DefaultQueryTimeZone = "UTC",
            CumulativeOrigin = CumulativeOrigins.OutputRangeStart
        };
    }

    /// <summary>Whether the result has an error for a property path, however the validator names properties.</summary>
    private static bool HasError(TestValidationResult<BindingInput> result, string path)
    {
        return result.Errors.Any(error => string.Equals(error.PropertyName, path, StringComparison.OrdinalIgnoreCase));
    }
}
