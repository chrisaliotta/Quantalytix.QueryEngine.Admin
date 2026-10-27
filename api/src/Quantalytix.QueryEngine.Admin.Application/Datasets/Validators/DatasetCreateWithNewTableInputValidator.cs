using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The shape of a dataset over a new managed table: a dataset key, a display name, a table name and columns named once, each typed
/// like a field. A taken key or table name is a conflict the command decides.
/// </summary>
public sealed class DatasetCreateWithNewTableInputValidator : AbstractValidator<DatasetCreateWithNewTableInput>
{
    /// <summary>Declares the rules.</summary>
    public DatasetCreateWithNewTableInputValidator()
    {
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeDatasetKey();
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.TableName).Cascade(CascadeMode.Stop).MustBeTableName();
        RuleForEach(input => input.Columns).SetValidator(new DatasetNewColumnInputValidator());
        RuleFor(input => input.Columns)
            .Must(columns => columns.DistinctBy(column => column.Name, StringComparer.OrdinalIgnoreCase).Count() == columns.Count)
            .WithMessage("Name each column once.");
    }
}
