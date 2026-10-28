using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Checks each analytical bridge operation's input shape before it applies: ids that are stored or temporary, names, member keys,
/// factors and weights that fit their columns, settings the cardinality allows (B5), and no descendants selection, which only a
/// report bridge takes (B8). Whether the mappings comply with the settings and the endpoints is for the rules.
/// </summary>
public sealed class BridgeOperationValidator : AbstractValidator<BridgeOperation>
{
    /// <summary>Declares the rules.</summary>
    public BridgeOperationValidator()
    {
        RuleFor(operation => operation).SetInheritanceValidator(operations =>
        {
            operations.Add(new InlineValidator<BridgeUpdateOperation>
            {
                validator => validator.RuleFor(operation => operation.Patch)
                    .Must(patch => patch.DisplayName is not null || patch.IsEnabled is not null)
                    .WithMessage("Name at least one field to change."),
                validator => validator.RuleFor(operation => operation.Patch.DisplayName).NotEmpty().MustFitVarchar(256).When(operation => operation.Patch.DisplayName is not null)
            });
            operations.Add(Targeting<BridgeVersionUpdateOperation>(validator =>
            {
                validator.RuleFor(operation => operation.Patch.Cardinality).MustBeIn(Cardinalities.Definition);
                validator.RuleFor(operation => operation.Patch.AllocationMode)
                    .Cascade(CascadeMode.Stop)
                    .MustBeIn(AllocationModes.Definition)
                    .Must((operation, _) => BridgeSettings.Allows(operation.Patch.Cardinality, operation.Patch.AllocationMode))
                    .WithMessage(operation => $"Cardinality {operation.Patch.Cardinality} does not allow allocation mode {operation.Patch.AllocationMode}.");
            }));
            operations.Add(Targeting<BridgeMappingSaveOperation>(validator => validator.RuleFor(operation => operation.Input).SetValidator(new MappingValidator())));
            operations.Add(Targeting<BridgeMappingDeleteOperation>(validator => validator.RuleFor(operation => operation.MappingId).NotEmpty().MustBeChangeSetId()));
            operations.Add(Targeting<BridgeMappingsReplaceOperation>(validator =>
            {
                validator.RuleForEach(operation => operation.Rows).SetValidator(new MappingValidator());
                validator.RuleFor(operation => operation.Rows)
                    .Must(rows => rows.DistinctBy(row => row.Id).Count() == rows.Count)
                    .WithMessage("'{PropertyName}' must name each mapping id once.");
            }));
        });
    }

    /// <summary>A validator of a version operation: its version id, then the operation's own rules.</summary>
    private static InlineValidator<TOperation> Targeting<TOperation>(Action<InlineValidator<TOperation>> rules) where TOperation : BridgeVersionOperation
    {
        var validator = new InlineValidator<TOperation>();
        validator.RuleFor(operation => operation.VersionId).NotEmpty().MustBeChangeSetId();
        rules(validator);
        return validator;
    }

    /// <summary>Checks one mapping input of an analytical bridge.</summary>
    private sealed class MappingValidator : AbstractValidator<BridgeMappingInput>
    {
        /// <summary>Declares the rules.</summary>
        public MappingValidator()
        {
            RuleFor(input => input.Id).NotEmpty().MustBeChangeSetId();
            RuleFor(input => input.SourceMemberKey).MustBeMemberKey();
            RuleFor(input => input.TargetMemberKey).MustBeMemberKey();
            RuleFor(input => input.Factor).MustFitDecimal();
            RuleFor(input => input.AllocationWeight).MustFitDecimal();
            RuleFor(input => input.SourceSelection)
                .Equal(SourceSelections.Member)
                .WithMessage("An analytical bridge maps each source member alone; only a report source's bridge maps a member's descendants.")
                .When(input => input.SourceSelection is not null);
        }
    }
}
