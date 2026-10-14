using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms the bulk bridge mapping check: a source and target pair listed twice is rejected, because the pair is the
/// table's primary key and the second row would silently replace the first; a negative weight and a weight with more than
/// the column's 12 decimals are rejected; signed factors pass.
/// </summary>
public sealed class BridgeMappingsInputValidatorTests
{
    /// <summary>The validator under test.</summary>
    private readonly BridgeMappingsInputValidator _validator = new();

    /// <summary>Distinct pairs with a negative factor and fractional weights pass.</summary>
    [Fact]
    public void DistinctPairs_Pass()
    {
        var input = Mappings(Mapping(1, 11, 0.4m, factor: -1m), Mapping(1, 12, 0.6m), Mapping(2, 11, 1m));

        _validator.TestValidate(input).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A pair listed twice fails, naming both members.</summary>
    [Fact]
    public void RepeatedPair_Fails()
    {
        var result = _validator.TestValidate(Mappings(Mapping(1, 11, 0.4m), Mapping(1, 11, 0.6m)));

        result.ShouldHaveValidationErrorFor(input => input.Mappings)
            .WithErrorMessage("Source member 1 is mapped to target member 11 more than once.");
    }

    /// <summary>A negative weight and a weight finer than 12 decimals fail on their mappings.</summary>
    [Fact]
    public void NegativeAndOverlyPreciseWeights_Fail()
    {
        var result = _validator.TestValidate(Mappings(Mapping(1, 11, -0.5m), Mapping(1, 12, 0.0000000000001m)));

        result.ShouldHaveValidationErrorFor("Mappings[0].AllocationWeight");
        result.ShouldHaveValidationErrorFor("Mappings[1].AllocationWeight");
    }

    /// <summary>A mapping list holding the given mappings.</summary>
    private static BridgeMappingsInput Mappings(params BridgeMappingInput[] mappings)
    {
        return new BridgeMappingsInput { Mappings = mappings };
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
