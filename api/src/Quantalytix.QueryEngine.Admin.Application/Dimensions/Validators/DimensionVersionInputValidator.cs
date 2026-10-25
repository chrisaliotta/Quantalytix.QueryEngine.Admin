using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a dimension version's attributes: a known range data type. <see cref="NewDimensionVersionInputValidator"/> includes
/// this rule.
/// </summary>
public sealed class DimensionVersionInputValidator : AbstractValidator<DimensionVersionInput>
{
    /// <summary>Declares the rules.</summary>
    public DimensionVersionInputValidator()
    {
        RuleFor(input => input.RangeDataType).MustBeIn(RangeDataTypes.Definition);
    }
}
