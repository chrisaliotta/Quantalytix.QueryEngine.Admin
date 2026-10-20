using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms the bulk bridge mapping check: a source and target pair listed twice is rejected, because the pair is unique
/// within a version; a negative weight and a weight with more than the column's 12 decimals are rejected; signed factors
/// pass.
/// </summary>
public sealed class BridgeMappingsInputValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly BridgeMappingsInputValidator _validator = new();

    /// <summary>Distinct pairs with a negative factor and fractional weights pass.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task DistinctPairs_Pass()
    {
        var result = await ValidateAsync(Mapping("LOAN_A", "EAST", 0.4m, factor: -1m), Mapping("LOAN_A", "WEST", 0.6m), Mapping("LOAN_B", "EAST", 1m));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A pair listed twice fails, naming both members.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task RepeatedPair_Fails()
    {
        var result = await ValidateAsync(Mapping("LOAN_A", "EAST", 0.4m), Mapping("LOAN_A", "EAST", 0.6m));

        result.ShouldHaveValidationErrorFor(input => input.Mappings)
            .WithErrorMessage("Source member 'LOAN_A' is mapped to target member 'EAST' more than once.");
    }

    /// <summary>A negative weight and a weight finer than 12 decimals fail on their mappings.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task NegativeAndOverlyPreciseWeights_Fail()
    {
        var result = await ValidateAsync(Mapping("LOAN_A", "EAST", -0.5m), Mapping("LOAN_A", "WEST", 0.0000000000001m));

        result.ShouldHaveValidationErrorFor("Mappings[0].AllocationWeight");
        result.ShouldHaveValidationErrorFor("Mappings[1].AllocationWeight");
    }

    /// <summary>Validates a mapping list holding the given mappings.</summary>
    private Task<TestValidationResult<BridgeMappingsInput>> ValidateAsync(params BridgeMappingInput[] mappings)
    {
        return _validator.TestValidateAsync(new BridgeMappingsInput { Mappings = mappings }, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>A mapping from a source member key to a target member key with a weight and a factor of 1 by default.</summary>
    private static BridgeMappingInput Mapping(string sourceMemberKey, string targetMemberKey, decimal allocationWeight, decimal factor = 1m)
    {
        return new BridgeMappingInput
        {
            SourceMemberKey = sourceMemberKey,
            TargetMemberKey = targetMemberKey,
            Factor = factor,
            AllocationWeight = allocationWeight
        };
    }
}
