using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms the bulk entry list check: source values are compared exactly, so <c>003000</c> and <c>3000</c> both pass, a
/// value listed twice is rejected once by name, an empty value is rejected, and a member that does not exist is named. A
/// bulk paste replaces every entry of the draft, so these are caught before anything is deleted.
/// </summary>
public sealed class MappingEntriesInputValidatorTests
{
    /// <summary>The validator under test, over a table holding members 1 and 2.</summary>
    private readonly MappingEntriesInputValidator _validator = new(new InMemoryTableGateway<DimensionMemberRecord>(Member(1, 10, "CA"), Member(2, 10, "NY")));

    /// <summary>Values that differ only by leading zeros are different source values.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task ValuesDifferingByLeadingZeros_Pass()
    {
        var result = await ValidateAsync(("003000", 1), ("3000", 1), ("06", 2));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A value listed twice fails once, naming the value.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task RepeatedValue_FailsOnce()
    {
        var result = await ValidateAsync(("06", 1), ("36", 2), ("06", 2));

        result.ShouldHaveValidationErrorFor(input => input.Entries).WithErrorMessage("Source key '06' appears more than once.");
        Assert.Single(result.Errors);
    }

    /// <summary>An empty value fails on its entry, and a missing member is named once however often it is used.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task EmptyValueAndMissingMember_Fail()
    {
        var result = await ValidateAsync(("06", 1), ("", 2), ("48", 9), ("49", 9));

        result.ShouldHaveValidationErrorFor("Entries[1].SourceKeyValue");
        result.ShouldHaveValidationErrorFor(input => input.Entries).WithErrorMessage("Member 9 does not exist.");
        Assert.Equal(2, result.Errors.Count);
    }

    /// <summary>Validates an entry list of the given source values and members.</summary>
    private Task<TestValidationResult<MappingEntriesInput>> ValidateAsync(params (string SourceKey, long MemberId)[] entries)
    {
        var input = new MappingEntriesInput
        {
            Entries = [.. entries.Select(entry => new MappingEntryInput { SourceKeyValue = entry.SourceKey, MemberId = entry.MemberId })]
        };
        return _validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
    }
}
