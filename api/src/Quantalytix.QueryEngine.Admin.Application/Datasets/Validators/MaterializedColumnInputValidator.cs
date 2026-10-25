using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Requires a well-formed column name when one is given; null clears the materialized column.</summary>
public sealed class MaterializedColumnInputValidator : AbstractValidator<MaterializedColumnInput>
{
    /// <summary>Declares the rules.</summary>
    public MaterializedColumnInputValidator()
    {
        RuleFor(input => input.ColumnName)
            .MaximumLength(128)
            .Matches(DatasetPatterns.ColumnName())
            .WithMessage(DatasetPatterns.ColumnNameMessage);
    }
}
