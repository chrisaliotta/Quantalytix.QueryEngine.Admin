using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Accepts only a known origin.</summary>
public sealed class DimensionListQueryValidator : AbstractValidator<DimensionListQuery>
{
    /// <summary>Declares the rules.</summary>
    public DimensionListQueryValidator()
    {
        RuleFor(query => query.Origin).MustBeIn(DefinitionOrigins.Definition);
    }
}
