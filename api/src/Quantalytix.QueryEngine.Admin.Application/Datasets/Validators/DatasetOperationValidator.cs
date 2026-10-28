using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Checks each dataset operation's input shape before it applies: ids that are stored or temporary, keys and names that fit their
/// columns, known vocabulary values, and the per-kind shape of fields, bindings and measures. What the input means for the version
/// (a taken key, a field outside the version, an unmanaged table) is decided as the operation applies.
/// </summary>
public sealed class DatasetOperationValidator : AbstractValidator<DatasetOperation>
{
    /// <summary>Declares the rules.</summary>
    public DatasetOperationValidator()
    {
        RuleFor(operation => operation).SetInheritanceValidator(operations =>
        {
            operations.Add(new InlineValidator<DatasetUpdateDatasetOperation>
            {
                validator => validator.RuleFor(operation => operation.Patch)
                    .Must(patch => patch.DisplayName is not null || patch.IsEnabled is not null)
                    .WithMessage("Name at least one field to change."),
                validator => validator.RuleFor(operation => operation.Patch.DisplayName).NotEmpty().MustFitVarchar(256).When(operation => operation.Patch.DisplayName is not null)
            });
            operations.Add(new InlineValidator<DatasetSaveSettingsOperation>
            {
                validator => validator.RuleFor(operation => operation.Settings).SetValidator(new DatasetVersionSettingsValidator())
            });
            operations.Add(new InlineValidator<DatasetRenameSourceTableOperation>
            {
                validator => validator.RuleFor(operation => operation.TableName).Cascade(CascadeMode.Stop).MustBeTableName()
            });
            operations.Add(new InlineValidator<DatasetSaveFieldOperation>
            {
                validator => validator.RuleFor(operation => operation.Input).SetValidator(new DatasetFieldInputValidator())
            });
            operations.Add(new InlineValidator<DatasetDeleteFieldOperation>
            {
                validator => validator.RuleFor(operation => operation.FieldId).NotEmpty().MustBeChangeSetId()
            });
            operations.Add(new InlineValidator<DatasetAddFieldsFromColumnsOperation>
            {
                validator => validator.RuleFor(operation => operation.Columns).NotEmpty().WithMessage("Choose at least one column."),
                validator => validator.RuleForEach(operation => operation.Columns).NotEmpty().MustBeIdentifier()
            });
            operations.Add(new InlineValidator<DatasetSaveGrainOperation>
            {
                validator => validator.RuleFor(operation => operation.Input.Id).MustBeChangeSetId(),
                validator => validator.RuleFor(operation => operation.Input.Key).Cascade(CascadeMode.Stop).MustBeKey(128),
                validator => validator.RuleFor(operation => operation.Input.GrainKind).MustBeIn(GrainKinds.Definition),
                validator => validator.RuleFor(operation => operation.Input.FieldIds)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .WithMessage("Add at least one component field.")
                    .Must(fieldIds => fieldIds.Distinct(StringComparer.Ordinal).Count() == fieldIds.Count)
                    .WithMessage("A field can appear only once."),
                validator => validator.RuleForEach(operation => operation.Input.FieldIds).NotEmpty().MustBeChangeSetId()
            });
            operations.Add(new InlineValidator<DatasetDeleteGrainOperation>
            {
                validator => validator.RuleFor(operation => operation.GrainId).NotEmpty().MustBeChangeSetId()
            });
            operations.Add(new InlineValidator<DatasetSaveBindingOperation>
            {
                validator => validator.RuleFor(operation => operation.Input).SetValidator(new DatasetBindingInputValidator())
            });
            operations.Add(new InlineValidator<DatasetDeleteBindingOperation>
            {
                validator => validator.RuleFor(operation => operation.BindingId).NotEmpty().MustBeChangeSetId()
            });
            operations.Add(new InlineValidator<DatasetSaveMeasureOperation>
            {
                validator => validator.RuleFor(operation => operation.Input).SetValidator(new DatasetMeasureInputValidator())
            });
            operations.Add(new InlineValidator<DatasetDeleteMeasureOperation>
            {
                validator => validator.RuleFor(operation => operation.MeasureId).NotEmpty().MustBeChangeSetId()
            });
        });
    }
}
