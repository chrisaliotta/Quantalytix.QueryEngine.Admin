using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms the bulk bridge mapping check: a source and target pair listed twice is rejected, because the pair is the
/// table's primary key and the second row would silently replace the first; a negative weight and a weight with more than
/// the column's 12 decimals are rejected; signed factors pass.
/// </summary>
public sealed class BridgeMappingsInputValidatorTests
{
    /// <summary>The validator under test, over a table holding source members 1 and 2 and target members 11 and 12.</summary>
    private readonly BridgeMappingsInputValidator _validator = new(new InMemoryTableGateway<DimensionMemberRecord>(
        Member(1, 10, "LOAN_A"),
        Member(2, 10, "LOAN_B"),
        Member(11, 20, "EAST"),
        Member(12, 20, "WEST")));

    /// <summary>Distinct pairs with a negative factor and fractional weights pass.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task DistinctPairs_Pass()
    {
        var result = await ValidateAsync(Mapping(1, 11, 0.4m, factor: -1m), Mapping(1, 12, 0.6m), Mapping(2, 11, 1m));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A pair listed twice fails, naming both members.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task RepeatedPair_Fails()
    {
        var result = await ValidateAsync(Mapping(1, 11, 0.4m), Mapping(1, 11, 0.6m));

        result.ShouldHaveValidationErrorFor(input => input.Mappings)
            .WithErrorMessage("Source member 1 is mapped to target member 11 more than once.");
    }

    /// <summary>A negative weight and a weight finer than 12 decimals fail on their mappings.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task NegativeAndOverlyPreciseWeights_Fail()
    {
        var result = await ValidateAsync(Mapping(1, 11, -0.5m), Mapping(1, 12, 0.0000000000001m));

        result.ShouldHaveValidationErrorFor("Mappings[0].AllocationWeight");
        result.ShouldHaveValidationErrorFor("Mappings[1].AllocationWeight");
    }

    /// <summary>Validates a mapping list holding the given mappings.</summary>
    private Task<TestValidationResult<BridgeMappingsInput>> ValidateAsync(params BridgeMappingInput[] mappings)
    {
        return _validator.TestValidateAsync(new BridgeMappingsInput { Mappings = mappings }, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>A mapping from a source member to a target member with a weight and a factor of 1 by default.</summary>
    private static BridgeMappingInput Mapping(long sourceMemberId, long targetMemberId, decimal allocationWeight, decimal factor = 1m)
    {
        return new BridgeMappingInput
        {
            SourceMemberId = sourceMemberId,
            TargetMemberId = targetMemberId,
            Factor = factor,
            AllocationWeight = allocationWeight
        };
    }
}
