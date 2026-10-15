using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Checks a new measure: a well-formed key plus every rule of <see cref="MeasureUpdateInputValidator"/>. Whether the key is
/// unique within the version is the <c>measure_key_unique</c> publication rule, because the version comes from the route.
/// </summary>
public sealed class MeasureInputValidator : AbstractValidator<MeasureInput>
{
    /// <summary>Declares the rules.</summary>
    public MeasureInputValidator()
    {
        RuleFor(input => input.MeasureKey).Cascade(CascadeMode.Stop).MustBeKey(128);
        Include(new MeasureUpdateInputValidator());
    }
}
