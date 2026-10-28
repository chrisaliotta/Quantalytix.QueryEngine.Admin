using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// The shape of a binding input: a time binding names its time dimension and carries its time axis; every other kind names a role;
/// each kind names exactly the references it reads (mapping set, bridge and source binding), and the unknown member only when
/// unmapped rows route to it.
/// </summary>
public sealed class DatasetBindingInputValidator : AbstractValidator<DatasetBindingInput>
{
    /// <summary>The kinds that read a source field.</summary>
    private static readonly string[] SourceBackedKinds = [BindingKinds.Direct, BindingKinds.Mapped, BindingKinds.Range, BindingKinds.Time];

    /// <summary>Declares the rules.</summary>
    public DatasetBindingInputValidator()
    {
        RuleFor(input => input.Id).MustBeChangeSetId();
        RuleFor(input => input.BindingKind).MustBeIn(BindingKinds.Definition);
        RuleFor(input => input.DimensionRoleId).MustBeChangeSetId();
        RuleFor(input => input.DimensionId).MustBeChangeSetId();
        RuleFor(input => input.SourceFieldId).MustBeChangeSetId();
        RuleFor(input => input.MappingSetId).MustBeChangeSetId();
        RuleFor(input => input.BridgeId).MustBeChangeSetId();
        RuleFor(input => input.SourceDimensionBindingId).MustBeChangeSetId();
        RuleFor(input => input.RaggedBehavior).MustBeIn(RaggedBehaviors.Definition);
        RuleFor(input => input.DefaultPeriodAlignment).MustBeIn(PeriodAlignments.Definition);
        RuleFor(input => input.UnmappedBehavior).MustBeIn(UnmappedBehaviors.Definition);

        When(input => input.BindingKind == BindingKinds.Time, () =>
        {
            RuleFor(input => input.DimensionId).NotNull().WithMessage(KindRequires(BindingKinds.Time));
            RuleFor(input => input.DimensionRoleId).Null().WithMessage("A time binding names its time dimension, so '{PropertyName}' must be empty.");
            RuleFor(input => input.Time).NotNull().WithMessage(KindRequires(BindingKinds.Time));
            RuleFor(input => input.Time!).SetValidator(new DatasetTimeBindingValidator());
        }).Otherwise(() =>
        {
            RuleFor(input => input.DimensionRoleId).NotNull().WithMessage(input => KindRequires(input.BindingKind));
            RuleFor(input => input.DimensionId).Null().WithMessage("The binding classifies into its role's dimension, so '{PropertyName}' must be empty.");
            RuleFor(input => input.Time).Null().WithMessage("Only time bindings have a time axis, so '{PropertyName}' must be empty.");
        });

        RuleFor(input => input.SourceFieldId)
            .NotNull()
            .WithMessage(input => KindRequires(input.BindingKind))
            .When(input => SourceBackedKinds.Contains(input.BindingKind));
        When(input => input.BindingKind == BindingKinds.Mapped, () =>
        {
            RuleFor(input => input.MappingSetId).NotNull().WithMessage(KindRequires(BindingKinds.Mapped));
        }).Otherwise(() =>
        {
            RuleFor(input => input.MappingSetId).Null().WithMessage(OnlyKindUses(BindingKinds.Mapped));
        });
        When(input => input.BindingKind == BindingKinds.Bridge, () =>
        {
            RuleFor(input => input.SourceDimensionBindingId).NotNull().WithMessage(KindRequires(BindingKinds.Bridge));
            RuleFor(input => input.BridgeId).NotNull().WithMessage(KindRequires(BindingKinds.Bridge));
        }).Otherwise(() =>
        {
            RuleFor(input => input.SourceDimensionBindingId).Null().WithMessage(OnlyKindUses(BindingKinds.Bridge));
            RuleFor(input => input.BridgeId).Null().WithMessage(OnlyKindUses(BindingKinds.Bridge));
        });

        When(input => input.UnmappedBehavior == UnmappedBehaviors.Unknown, () =>
        {
            RuleFor(input => input.UnknownMemberKey).NotEmpty().WithMessage("'{PropertyName}' is required when unmapped rows route to the unknown member.").MustFitVarchar(256);
        }).Otherwise(() =>
        {
            RuleFor(input => input.UnknownMemberKey).Null().WithMessage("Only the unknown unmapped behavior routes rows to a member, so '{PropertyName}' must be empty.");
        });
    }

    /// <summary>The message of a reference a kind requires.</summary>
    private static string KindRequires(string bindingKind)
    {
        return $"'{{PropertyName}}' is required for a {bindingKind} binding.";
    }

    /// <summary>The message of a reference only one kind reads.</summary>
    private static string OnlyKindUses(string bindingKind)
    {
        return $"Only {bindingKind} bindings use '{{PropertyName}}', so it must be empty.";
    }
}
