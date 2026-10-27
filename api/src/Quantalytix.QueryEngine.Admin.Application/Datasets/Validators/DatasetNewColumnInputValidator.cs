using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The shape of a new physical column: a name that can be inlined into DDL, typed like a field.</summary>
public sealed class DatasetNewColumnInputValidator : AbstractValidator<DatasetNewColumnInput>
{
    /// <summary>Declares the rules.</summary>
    public DatasetNewColumnInputValidator()
    {
        RuleFor(column => column.Name).Cascade(CascadeMode.Stop).NotEmpty().MustBeIdentifier();
        this.RequireFieldType(column => column.DataType, column => column.NumericPrecision, column => column.NumericScale);
    }
}
