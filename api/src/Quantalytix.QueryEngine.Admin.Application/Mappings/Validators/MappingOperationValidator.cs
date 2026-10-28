using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks each mapping set operation's input shape before it applies: ids that are stored or temporary, names that fit their columns,
/// and source values and member keys that fit theirs. Whether the entries comply (B2, B3) is for the rules.
/// </summary>
public sealed class MappingOperationValidator : AbstractValidator<MappingOperation>
{
    /// <summary>Declares the rules.</summary>
    public MappingOperationValidator()
    {
        RuleFor(operation => operation).SetInheritanceValidator(operations =>
        {
            operations.Add(new InlineValidator<MappingSetUpdateOperation>
            {
                validator => validator.RuleFor(operation => operation.Patch)
                    .Must(patch => patch.DisplayName is not null || patch.SourceNamespace is not null)
                    .WithMessage("Name at least one field to change."),
                validator => validator.RuleFor(operation => operation.Patch.DisplayName).NotEmpty().MustFitVarchar(256).When(operation => operation.Patch.DisplayName is not null),
                validator => validator.RuleFor(operation => operation.Patch.SourceNamespace).NotEmpty().MustFitVarchar(128).When(operation => operation.Patch.SourceNamespace is not null)
            });
            operations.Add(Targeting<MappingEntrySaveOperation>(validator => validator.RuleFor(operation => operation.Input).SetValidator(new EntryValidator())));
            operations.Add(Targeting<MappingEntryDeleteOperation>(validator => validator.RuleFor(operation => operation.EntryId).NotEmpty().MustBeChangeSetId()));
            operations.Add(Targeting<MappingEntriesReplaceOperation>(validator =>
            {
                validator.RuleForEach(operation => operation.Entries).SetValidator(new EntryValidator());
                validator.RuleFor(operation => operation.Entries)
                    .Must(entries => entries.DistinctBy(entry => entry.Id).Count() == entries.Count)
                    .WithMessage("'{PropertyName}' must name each entry id once.");
            }));
        });
    }

    /// <summary>A validator of a version operation: its version id, then the operation's own rules.</summary>
    private static InlineValidator<TOperation> Targeting<TOperation>(Action<InlineValidator<TOperation>> rules) where TOperation : MappingVersionOperation
    {
        var validator = new InlineValidator<TOperation>();
        validator.RuleFor(operation => operation.VersionId).NotEmpty().MustBeChangeSetId();
        rules(validator);
        return validator;
    }

    /// <summary>Checks one entry input.</summary>
    private sealed class EntryValidator : AbstractValidator<MappingEntryInput>
    {
        /// <summary>Declares the rules.</summary>
        public EntryValidator()
        {
            RuleFor(input => input.Id).NotEmpty().MustBeChangeSetId();
            RuleFor(input => input.SourceKeyValue).MustBeSourceKeyValue();
            RuleFor(input => input.MemberKey).MustBeMemberKey();
        }
    }
}
