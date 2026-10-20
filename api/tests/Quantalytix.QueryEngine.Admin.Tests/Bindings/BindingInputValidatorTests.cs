using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms the binding input rules of schema sections 11 and 12: a breakout binding binds an existing role and a time binding
/// names its dimension, each binding kind requires its own references and forbids the others', the unknown unmapped behavior
/// needs its member, a required binding stays enabled, and a time binding's extension carries valid periodicities, IANA time
/// zones the query engine accepts, a source zone exactly for timestamps and a fixed start exactly for that origin. The engine
/// resolves bindings from exactly these columns, so a binding that breaks them misroutes or fails at query time.
/// </summary>
public sealed class BindingInputValidatorTests
{
    /// <summary>The validator under test, over role 81 of dimension 7000.</summary>
    private readonly BindingInputValidator _validator = new(new InMemoryTableGateway<DimensionRoleRecord>(DimensionRows.Role(81, "branch", 7000)));

    /// <summary>A valid binding of each kind passes.</summary>
    /// <param name="kind">The binding's kind.</param>
    /// <returns>A task that completes when the input is validated.</returns>
    [Theory]
    [InlineData(BindingKinds.Direct)]
    [InlineData(BindingKinds.Mapped)]
    [InlineData(BindingKinds.Range)]
    [InlineData(BindingKinds.Bridge)]
    [InlineData(BindingKinds.Time)]
    public async Task ValidBindingOfEachKind_Passes(string kind)
    {
        (await ValidateAsync(Binding(kind))).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// A breakout binding binds an existing role, whose template the service copies into the dimension, so it names no
    /// dimension of its own; the error lands on the property the user set.
    /// </summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task BreakoutBinding_BindsAnExistingRoleAndNoDimension()
    {
        (await ValidateAsync(Binding(BindingKinds.Direct) with { RoleId = null })).ShouldHaveValidationErrorFor(binding => binding.RoleId);
        (await ValidateAsync(Binding(BindingKinds.Direct) with { RoleId = 99 })).ShouldHaveValidationErrorFor(binding => binding.RoleId);
        (await ValidateAsync(Binding(BindingKinds.Direct) with { DimensionId = 7000 })).ShouldHaveValidationErrorFor(binding => binding.DimensionId);
    }

    /// <summary>A time binding names its time dimension and no role, because time dimensions have no roles.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task TimeBinding_NamesItsDimensionAndNoRole()
    {
        (await ValidateAsync(Binding(BindingKinds.Time) with { DimensionId = null })).ShouldHaveValidationErrorFor(binding => binding.DimensionId);
        (await ValidateAsync(Binding(BindingKinds.Time) with { RoleId = 81 })).ShouldHaveValidationErrorFor(binding => binding.RoleId);
    }

    /// <summary>Every kind but bridge classifies a source field's value, so it needs the field; a bridge binding may omit it.</summary>
    /// <param name="kind">The binding's kind.</param>
    /// <param name="required">Whether the source field is required.</param>
    /// <returns>A task that completes when the input is validated.</returns>
    [Theory]
    [InlineData(BindingKinds.Direct, true)]
    [InlineData(BindingKinds.Mapped, true)]
    [InlineData(BindingKinds.Range, true)]
    [InlineData(BindingKinds.Time, true)]
    [InlineData(BindingKinds.Bridge, false)]
    public async Task SourceField_IsRequiredExceptForBridges(string kind, bool required)
    {
        var result = await ValidateAsync(Binding(kind) with { SourceFieldId = null });

        Assert.Equal(required, HasError(result, nameof(BindingInput.SourceFieldId)));
    }

    /// <summary>A mapped binding needs its mapping set, and no other kind may name one.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task MappingSet_IsRequiredForMappedAndForbiddenOtherwise()
    {
        (await ValidateAsync(Binding(BindingKinds.Mapped) with { MappingSetId = null })).ShouldHaveValidationErrorFor(binding => binding.MappingSetId);
        (await ValidateAsync(Binding(BindingKinds.Direct) with { MappingSetId = 30 })).ShouldHaveValidationErrorFor(binding => binding.MappingSetId);
    }

    /// <summary>A bridge binding needs its source binding and bridge version, and no other kind may name either.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task BridgeReferences_AreRequiredForBridgeAndForbiddenOtherwise()
    {
        var missing = await ValidateAsync(Binding(BindingKinds.Bridge) with { SourceBindingId = null, BridgeId = null });
        var foreign = await ValidateAsync(Binding(BindingKinds.Mapped) with { SourceBindingId = 21, BridgeId = 40 });

        missing.ShouldHaveValidationErrorFor(binding => binding.SourceBindingId);
        missing.ShouldHaveValidationErrorFor(binding => binding.BridgeId);
        foreign.ShouldHaveValidationErrorFor(binding => binding.SourceBindingId);
        foreign.ShouldHaveValidationErrorFor(binding => binding.BridgeId);
    }

    /// <summary>A time binding needs its time extension, and no other kind may send one.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task TimeExtension_IsRequiredForTimeAndForbiddenOtherwise()
    {
        (await ValidateAsync(Binding(BindingKinds.Time) with { TimeBinding = null })).ShouldHaveValidationErrorFor(binding => binding.TimeBinding);
        (await ValidateAsync(Binding(BindingKinds.Direct) with { TimeBinding = TimeBinding() })).ShouldHaveValidationErrorFor(binding => binding.TimeBinding);
    }

    /// <summary>Routing unmapped rows to the unknown member needs that member; other unmapped behaviors name none.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task UnknownMember_IsRequiredExactlyForTheUnknownBehavior()
    {
        var missing = Binding(BindingKinds.Mapped) with { UnmappedBehavior = UnmappedBehaviors.Unknown, UnknownMemberKey = null };
        var unused = Binding(BindingKinds.Mapped) with { UnmappedBehavior = UnmappedBehaviors.Exclude, UnknownMemberKey = "UNKNOWN" };

        (await ValidateAsync(missing)).ShouldHaveValidationErrorFor(binding => binding.UnknownMemberKey);
        (await ValidateAsync(unused)).ShouldHaveValidationErrorFor(binding => binding.UnknownMemberKey);
        (await ValidateAsync(missing with { UnknownMemberKey = "UNKNOWN" })).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A required binding is a dataset fixture that must not be disabled (schema section 11).</summary>
    /// <returns>A task that completes when the input is validated.</returns>
    [Fact]
    public async Task RequiredBinding_MustStayEnabled()
    {
        var input = Binding(BindingKinds.Direct) with { IsRequired = true, IsEnabled = false };

        (await ValidateAsync(input)).ShouldHaveValidationErrorFor(binding => binding.IsEnabled);
    }

    /// <summary>Kinds and behaviors must be vocabulary values.</summary>
    /// <returns>A task that completes when the input is validated.</returns>
    [Fact]
    public async Task UnknownVocabularyValues_AreRejected()
    {
        var input = Binding(BindingKinds.Direct) with
        {
            BindingKind = "lookup",
            RaggedBehavior = "skip",
            DefaultPeriodAlignment = "middle",
            UnmappedBehavior = "drop"
        };

        var result = await ValidateAsync(input);

        result.ShouldHaveValidationErrorFor(binding => binding.BindingKind);
        result.ShouldHaveValidationErrorFor(binding => binding.RaggedBehavior);
        result.ShouldHaveValidationErrorFor(binding => binding.DefaultPeriodAlignment);
        result.ShouldHaveValidationErrorFor(binding => binding.UnmappedBehavior);
    }

    /// <summary>The allowed periodicities are a non-empty set of distinct periodicity values.</summary>
    /// <param name="periodicities">The periodicities sent, comma-separated.</param>
    /// <param name="valid">Whether they are accepted.</param>
    /// <returns>A task that completes when the input is validated.</returns>
    [Theory]
    [InlineData("day,month,quarter,year", true)]
    [InlineData("month", true)]
    [InlineData("", false)]
    [InlineData("day,day", false)]
    [InlineData("day,week", false)]
    public async Task AllowedPeriodicities_AreADistinctNonEmptySubset(string periodicities, bool valid)
    {
        var allowed = periodicities.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = await ValidateAsync(Binding(BindingKinds.Time) with { TimeBinding = TimeBinding() with { AllowedPeriodicities = allowed } });

        Assert.Equal(!valid, result.Errors.Any(error => error.PropertyName.StartsWith("TimeBinding.AllowedPeriodicities", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Timestamps are recorded in a source time zone, which dates do not have.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task SourceTimeZone_IsRequiredForTimestampsAndForbiddenForDates()
    {
        var timestamp = Binding(BindingKinds.Time) with { TimeBinding = TimeBinding() with { TimeValueType = TimeValueTypes.Timestamp, SourceTimeZone = null } };
        var date = Binding(BindingKinds.Time) with { TimeBinding = TimeBinding() with { SourceTimeZone = "America/Chicago" } };

        (await ValidateAsync(timestamp)).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.SourceTimeZone);
        (await ValidateAsync(date)).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.SourceTimeZone);
    }

    /// <summary>
    /// Time zones must be IANA zones, the query engine's own test: a Windows zone name resolves on most systems but has no
    /// IANA name, so the engine would reject it.
    /// </summary>
    /// <param name="timeZone">The zone sent as both the source and the default query zone.</param>
    /// <param name="valid">Whether it is accepted.</param>
    /// <returns>A task that completes when the input is validated.</returns>
    [Theory]
    [InlineData("UTC", true)]
    [InlineData("America/Chicago", true)]
    [InlineData("Europe/London", true)]
    [InlineData("Central Standard Time", false)]
    [InlineData("Mars/Olympus_Mons", false)]
    public async Task TimeZones_MustBeIanaZones(string timeZone, bool valid)
    {
        var extension = TimeBinding() with { TimeValueType = TimeValueTypes.Timestamp, SourceTimeZone = timeZone, DefaultQueryTimeZone = timeZone };

        var result = await ValidateAsync(Binding(BindingKinds.Time) with { TimeBinding = extension });

        Assert.Equal(!valid, HasError(result, "TimeBinding.SourceTimeZone"));
        Assert.Equal(!valid, HasError(result, "TimeBinding.DefaultQueryTimeZone"));
    }

    /// <summary>The fixed start is required exactly for the fixed_start cumulative origin, and must be a UTC time.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task FixedStart_IsRequiredExactlyForTheFixedStartOrigin()
    {
        var missing = TimeBinding() with { CumulativeOrigin = CumulativeOrigins.FixedStart, CumulativeFixedStart = null };
        var unused = TimeBinding() with { CumulativeFixedStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var local = missing with { CumulativeFixedStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local) };

        (await ValidateAsync(Binding(BindingKinds.Time) with { TimeBinding = missing })).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.CumulativeFixedStart);
        (await ValidateAsync(Binding(BindingKinds.Time) with { TimeBinding = unused })).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.CumulativeFixedStart);
        (await ValidateAsync(Binding(BindingKinds.Time) with { TimeBinding = local })).ShouldHaveValidationErrorFor(binding => binding.TimeBinding!.CumulativeFixedStart);
    }

    /// <summary>Validates an input.</summary>
    private Task<TestValidationResult<BindingInput>> ValidateAsync(BindingInput input)
    {
        return _validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>A valid binding of a kind with the schema's default behaviors: of role 81, or of time dimension 7000 for <c>time</c>.</summary>
    private static BindingInput Binding(string kind)
    {
        var binding = new BindingInput
        {
            RoleId = 81,
            BindingKind = kind,
            SourceFieldId = 1,
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
            BindingKinds.Bridge => binding with { SourceFieldId = null, SourceBindingId = 21, BridgeId = 40 },
            BindingKinds.Time => binding with { RoleId = null, DimensionId = 7000, TimeBinding = TimeBinding() },
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
