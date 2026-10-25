using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Checks a bridge version's attributes: known cardinality and allocation values. <see cref="NewBridgeVersionInputValidator"/>
/// includes these rules.
/// </summary>
public sealed class BridgeVersionInputValidator : AbstractValidator<BridgeVersionInput>
{
    /// <summary>Declares the rules.</summary>
    public BridgeVersionInputValidator()
    {
        RuleFor(input => input.Cardinality).MustBeIn(Cardinalities.Definition);
        RuleFor(input => input.AllocationMode).MustBeIn(AllocationModes.Definition);
    }
}
