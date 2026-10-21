using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Accepts only a known dimension purpose, when the list is narrowed by one.</summary>
public sealed class BridgeListQueryValidator : AbstractValidator<BridgeListQuery>
{
    /// <summary>Declares the rules.</summary>
    public BridgeListQueryValidator()
    {
        RuleFor(query => query.Purpose).MustBeIn(DimensionPurposes.Definition);
    }
}
