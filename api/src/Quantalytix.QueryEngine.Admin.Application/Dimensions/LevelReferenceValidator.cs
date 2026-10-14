using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Accepts a level route only when the level belongs to the dimension version the route names.</summary>
public sealed class LevelReferenceValidator : AbstractValidator<LevelReference>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="levels">Reads the level.</param>
    public LevelReferenceValidator(ITableGateway<DimensionLevelRecord> levels)
    {
        RuleFor(reference => reference.LevelId)
            .MustAsync(async (reference, levelId, cancellationToken) =>
                (await levels.ListAsync(new { LevelId = levelId, reference.DimensionVersionId }, cancellationToken)).Count == 1)
            .WithMessage(reference => $"Level {reference.LevelId} is not a level of dimension version {reference.DimensionVersionId}.");
    }
}
