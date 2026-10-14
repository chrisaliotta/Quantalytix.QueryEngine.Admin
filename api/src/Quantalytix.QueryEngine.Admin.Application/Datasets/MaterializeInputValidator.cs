using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Requires a generated column name that is safe to inline into DDL.</summary>
public sealed class MaterializeInputValidator : AbstractValidator<MaterializeInput>
{
    /// <summary>Declares the rules.</summary>
    public MaterializeInputValidator()
    {
        RuleFor(input => input.ColumnName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(DatasetPatterns.ColumnName())
            .WithMessage(DatasetPatterns.ColumnNameMessage);
    }
}
