using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks the shape of each band: decimal bounds fit the <c>DECIMAL(38, 12)</c> columns and datetime bounds are UTC. Which
/// bound family a band uses, and how bands order and overlap, are publication rules, because they depend on the version.
/// </summary>
public sealed class RangeBandsInputValidator : AbstractValidator<RangeBandsInput>
{
    /// <summary>Declares the rules.</summary>
    public RangeBandsInputValidator()
    {
        RuleForEach(input => input.Bands).ChildRules(band =>
        {
            band.RuleFor(input => input.LowerDecimal).PrecisionScale(38, 12, true);
            band.RuleFor(input => input.UpperDecimal).PrecisionScale(38, 12, true);
            band.RuleFor(input => input.LowerDatetime).MustBeUtc();
            band.RuleFor(input => input.UpperDatetime).MustBeUtc();
        });
    }
}
