using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Checks the editable identity attributes of a dataset.</summary>
public sealed class DatasetUpdateInputValidator : AbstractValidator<DatasetUpdateInput>
{
    /// <summary>Declares the rules.</summary>
    public DatasetUpdateInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MaximumLength(256);
    }
}
