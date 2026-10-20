using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms the bulk entry list check: source values are compared exactly, so <c>003000</c> and <c>3000</c> both pass, a
/// value listed twice is rejected once by name, an empty value is rejected, a value longer than its VARCHAR(512) column in
/// UTF-8 bytes is rejected, and so is an empty member key. A bulk paste deletes every entry of the version before it inserts
/// the new list, so these are caught before anything is deleted. Whether a member holds the key is a publication rule.
/// </summary>
public sealed class MappingEntriesInputValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly MappingEntriesInputValidator _validator = new();

    /// <summary>Values that differ only by leading zeros are different source values.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task ValuesDifferingByLeadingZeros_Pass()
    {
        var result = await ValidateAsync(("003000", "CA"), ("3000", "CA"), ("06", "NY"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A value listed twice fails once, naming the value.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task RepeatedValue_FailsOnce()
    {
        var result = await ValidateAsync(("06", "CA"), ("36", "NY"), ("06", "NY"));

        result.ShouldHaveValidationErrorFor(input => input.Entries).WithErrorMessage("Source key '06' appears more than once.");
        Assert.Single(result.Errors);
    }

    /// <summary>An empty value and an empty member key each fail on their entry.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task EmptyValueAndEmptyMemberKey_Fail()
    {
        var result = await ValidateAsync(("06", "CA"), ("", "NY"), ("48", ""));

        result.ShouldHaveValidationErrorFor("Entries[1].SourceKeyValue");
        result.ShouldHaveValidationErrorFor("Entries[2].MemberKey");
        Assert.Equal(2, result.Errors.Count);
    }

    /// <summary>
    /// StarRocks counts the column's 512 in UTF-8 bytes: 257 two-byte characters fail on their entry, while 256 fill the
    /// column exactly and pass.
    /// </summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task ValueLongerThanTheColumnInBytes_Fails()
    {
        var result = await ValidateAsync((new string('é', 256), "CA"), (new string('é', 257), "NY"));

        result.ShouldHaveValidationErrorFor("Entries[1].SourceKeyValue");
        Assert.Single(result.Errors);
    }

    /// <summary>Validates an entry list of the given source values and member keys.</summary>
    private Task<TestValidationResult<MappingEntriesInput>> ValidateAsync(params (string SourceKey, string MemberKey)[] entries)
    {
        var input = new MappingEntriesInput
        {
            Entries = [.. entries.Select(entry => new MappingEntryInput { SourceKeyValue = entry.SourceKey, MemberKey = entry.MemberKey })]
        };
        return _validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
    }
}
