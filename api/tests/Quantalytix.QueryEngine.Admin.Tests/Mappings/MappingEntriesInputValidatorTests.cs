using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms the bulk entry list check: source values are compared exactly, so <c>003000</c> and <c>3000</c> both pass, a
/// value listed twice is rejected once by name, and an empty value is rejected. A bulk paste replaces every entry of the
/// draft, so a repeated value is caught before anything is deleted rather than at validation.
/// </summary>
public sealed class MappingEntriesInputValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly MappingEntriesInputValidator _validator = new();

    /// <summary>Values that differ only by leading zeros are different source values.</summary>
    [Fact]
    public void ValuesDifferingByLeadingZeros_Pass()
    {
        _validator.TestValidate(Entries("003000", "3000", "06")).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A value listed twice fails once, naming the value.</summary>
    [Fact]
    public void RepeatedValue_FailsOnce()
    {
        var result = _validator.TestValidate(Entries("06", "36", "06"));

        result.ShouldHaveValidationErrorFor(input => input.Entries).WithErrorMessage("Source key '06' appears more than once.");
        Assert.Single(result.Errors);
    }

    /// <summary>An empty value fails on its entry.</summary>
    [Fact]
    public void EmptyValue_Fails()
    {
        _validator.TestValidate(Entries("06", "")).ShouldHaveValidationErrorFor("Entries[1].SourceKeyValue");
    }

    /// <summary>An entry list with the given source values, each mapped to member 1.</summary>
    private static MappingEntriesInput Entries(params string[] sourceKeys)
    {
        return new MappingEntriesInput { Entries = [.. sourceKeys.Select(sourceKey => new MappingEntryInput { SourceKeyValue = sourceKey, MemberId = 1 })] };
    }
}
