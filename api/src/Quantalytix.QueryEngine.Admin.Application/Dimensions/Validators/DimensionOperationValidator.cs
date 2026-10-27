using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks each dimension operation's input shape before it applies: ids that are stored or temporary, well-formed keys for what an
/// operation creates, names that fit their columns, and known vocabulary values. What the input means for the aggregate (a taken
/// key, a parent outside the version) is decided as the operation applies.
/// </summary>
public sealed class DimensionOperationValidator : AbstractValidator<DimensionOperation>
{
    /// <summary>Declares the rules.</summary>
    public DimensionOperationValidator()
    {
        RuleFor(operation => operation).SetInheritanceValidator(operations =>
        {
            operations.Add(new InlineValidator<DimensionUpdateDimensionOperation>
            {
                validator => validator.RuleFor(operation => operation.Patch)
                    .Must(patch => patch.DisplayName is not null || patch.IsEnabled is not null || patch.IsRemovable is not null)
                    .WithMessage("Name at least one field to change."),
                validator => validator.RuleFor(operation => operation.Patch.DisplayName).NotEmpty().MustFitVarchar(256).When(operation => operation.Patch.DisplayName is not null)
            });
            operations.Add(Targeting<DimensionUpdateVersionOperation>(validator => validator.RuleFor(operation => operation.Patch.RangeDataType).MustBeIn(RangeDataTypes.Definition)));
            operations.Add(Targeting<DimensionSaveMemberOperation>(validator =>
            {
                validator.RuleFor(operation => operation.Input.Id).MustBeChangeSetId();
                validator.RuleFor(operation => operation.Input.Key).Cascade(CascadeMode.Stop).MustBeKey(256).When(operation => Creates(operation.Input.Id));
                validator.RuleFor(operation => operation.Input.DisplayName).NotEmpty().MustFitVarchar(512);
                validator.RuleFor(operation => operation.Input.Description).MustFitVarchar(1024);
                validator.RuleFor(operation => operation.Input.ParentId).MustBeChangeSetId();
                validator.RuleFor(operation => operation.Input.LevelId).MustBeChangeSetId();
                validator.RuleFor(operation => operation.Input.SortOrder).GreaterThanOrEqualTo(0);
            }));
            operations.Add(Targeting<DimensionDeleteMemberOperation>(validator => validator.RuleFor(operation => operation.MemberId).NotEmpty().MustBeChangeSetId()));
            operations.Add(Targeting<DimensionMoveMemberOperation>(validator =>
            {
                validator.RuleFor(operation => operation.MemberId).NotEmpty().MustBeChangeSetId();
                validator.RuleFor(operation => operation.To.ParentId).MustBeChangeSetId();
                validator.RuleFor(operation => operation.To.Index).GreaterThanOrEqualTo(0);
            }));
            operations.Add(Targeting<DimensionImportMembersOperation>(validator =>
            {
                validator.RuleFor(operation => operation.Mode).Must(DimensionImportModes.All.Contains).WithMessage("'{PropertyValue}' is not an import mode: merge or replace.");
                validator.RuleForEach(operation => operation.Rows).ChildRules(row =>
                {
                    row.RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeKey(256);
                    row.RuleFor(input => input.DisplayName).MustFitVarchar(512);
                    row.RuleFor(input => input.ParentKey).MustFitVarchar(256);
                });
            }));
            operations.Add(Targeting<DimensionClearMembersOperation>(_ => { }));
            operations.Add(Targeting<DimensionSaveLevelOperation>(validator =>
            {
                validator.RuleFor(operation => operation.Input.Id).MustBeChangeSetId();
                validator.RuleFor(operation => operation.Input.Key).Cascade(CascadeMode.Stop).MustBeKey(128).When(operation => Creates(operation.Input.Id));
                validator.RuleFor(operation => operation.Input.DisplayName).NotEmpty().MustFitVarchar(256);
                validator.RuleFor(operation => operation.Input.Periodicity).MustBeIn(Periodicities.Definition);
            }));
            operations.Add(Targeting<DimensionDeleteLevelOperation>(validator => validator.RuleFor(operation => operation.LevelId).NotEmpty().MustBeChangeSetId()));
            operations.Add(Targeting<DimensionReorderLevelsOperation>(validator => validator.RuleForEach(operation => operation.OrderedIds).NotEmpty().MustBeChangeSetId()));
            operations.Add(Targeting<DimensionSaveBandsOperation>(validator =>
                validator.RuleForEach(operation => operation.Bands).ChildRules(band => band.RuleFor(input => input.MemberId).NotEmpty().MustBeChangeSetId())));
            operations.Add(new InlineValidator<DimensionSaveRoleOperation>
            {
                validator => validator.RuleFor(operation => operation.Input.Id).MustBeChangeSetId(),
                validator => validator.RuleFor(operation => operation.Input.Key).Cascade(CascadeMode.Stop).MustBeKey(128).When(operation => Creates(operation.Input.Id)),
                validator => validator.RuleFor(operation => operation.Input.DisplayName).NotEmpty().MustFitVarchar(256),
                validator => validator.RuleFor(operation => operation.Input.Description).MustFitVarchar(1024)
            });
            operations.Add(new InlineValidator<DimensionDeleteRoleOperation>
            {
                validator => validator.RuleFor(operation => operation.RoleId).NotEmpty().MustBeChangeSetId()
            });
        });
    }

    /// <summary>A validator of a version operation: its version id, then the operation's own rules.</summary>
    private static InlineValidator<TOperation> Targeting<TOperation>(Action<InlineValidator<TOperation>> rules) where TOperation : DimensionVersionOperation
    {
        var validator = new InlineValidator<TOperation>();
        validator.RuleFor(operation => operation.VersionId).NotEmpty().MustBeChangeSetId();
        rules(validator);
        return validator;
    }

    /// <summary>Whether an input id creates an item: absent or temporary.</summary>
    private static bool Creates(string? id)
    {
        return id is null || TemporaryIds.IsTemporary(id);
    }
}
