using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Requires at least one column to create fields from.</summary>
public sealed class FieldsFromColumnsInputValidator : AbstractValidator<FieldsFromColumnsInput>
{
    /// <summary>Declares the rules.</summary>
    public FieldsFromColumnsInputValidator()
    {
        RuleFor(input => input.Columns).NotEmpty();
    }
}
