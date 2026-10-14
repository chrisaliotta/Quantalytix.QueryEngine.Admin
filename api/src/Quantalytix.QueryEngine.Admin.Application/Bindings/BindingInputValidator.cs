using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Checks a binding against schema section 11: vocabulary values, the references each binding kind requires and forbids,
/// the grouping reference each grouping mode needs, the unknown member of the <c>unknown</c> unmapped behavior, and that a
/// required binding stays enabled. <see cref="TimeBindingInputValidator"/> checks the time extension. Whether the referenced
/// rows belong to the version, the dimension and its versions is left to the publication rules, which see the route too.
/// </summary>
public sealed class BindingInputValidator : AbstractValidator<BindingInput>
{
    /// <summary>The binding kinds that classify the value of a source field, so they need one.</summary>
    private static readonly string[] SourceBackedKinds = [BindingKinds.Direct, BindingKinds.Mapped, BindingKinds.Range, BindingKinds.Time];

    /// <summary>Declares the rules.</summary>
    public BindingInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(input => input.DimensionId).NotNull();
        RuleFor(input => input.BindingKind).MustBeIn(BindingKinds.Definition);
        RuleFor(input => input.GroupingMode).MustBeIn(GroupingModes.Definition);
        RuleFor(input => input.RaggedBehavior).MustBeIn(RaggedBehaviors.Definition);
        RuleFor(input => input.DefaultPeriodAlignment).MustBeIn(PeriodAlignments.Definition);
        RuleFor(input => input.UnmappedBehavior).MustBeIn(UnmappedBehaviors.Definition);

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
            RuleFor(input => input.SourceBindingId).NotNull().WithMessage(KindRequires(BindingKinds.Bridge));
            RuleFor(input => input.BridgeVersionId).NotNull().WithMessage(KindRequires(BindingKinds.Bridge));
        }).Otherwise(() =>
        {
            RuleFor(input => input.SourceBindingId).Null().WithMessage(OnlyKindUses(BindingKinds.Bridge));
            RuleFor(input => input.BridgeVersionId).Null().WithMessage(OnlyKindUses(BindingKinds.Bridge));
        });
        When(input => input.BindingKind == BindingKinds.Time, () =>
        {
            RuleFor(input => input.TimeBinding).NotNull().WithMessage(KindRequires(BindingKinds.Time));
            RuleFor(input => input.TimeBinding!).SetValidator(new TimeBindingInputValidator());
        }).Otherwise(() =>
        {
            RuleFor(input => input.TimeBinding).Null().WithMessage("Only time bindings have a time extension, so '{PropertyName}' must be absent.");
        });

        When(input => input.GroupingMode == GroupingModes.Level, () =>
        {
            RuleFor(input => input.GroupingLevelId).NotNull().WithMessage(ModeRequires(GroupingModes.Level));
        }).Otherwise(() =>
        {
            RuleFor(input => input.GroupingLevelId).Null().WithMessage(OnlyModeUses(GroupingModes.Level));
        });
        When(input => input.GroupingMode == GroupingModes.Ancestor, () =>
        {
            RuleFor(input => input.GroupingAncestorMemberId).NotNull().WithMessage(ModeRequires(GroupingModes.Ancestor));
        }).Otherwise(() =>
        {
            RuleFor(input => input.GroupingAncestorMemberId).Null().WithMessage(OnlyModeUses(GroupingModes.Ancestor));
        });
        When(input => input.GroupingMode == GroupingModes.Depth, () =>
        {
            RuleFor(input => input.GroupingDepth)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage(ModeRequires(GroupingModes.Depth))
                .GreaterThanOrEqualTo(0);
        }).Otherwise(() =>
        {
            RuleFor(input => input.GroupingDepth).Null().WithMessage(OnlyModeUses(GroupingModes.Depth));
        });

        When(input => input.UnmappedBehavior == UnmappedBehaviors.Unknown, () =>
        {
            RuleFor(input => input.UnknownMemberId)
                .NotNull()
                .WithMessage("'{PropertyName}' is required when unmapped rows route to the unknown member.");
        }).Otherwise(() =>
        {
            RuleFor(input => input.UnknownMemberId)
                .Null()
                .WithMessage("Only the unknown unmapped behavior routes rows to a member, so '{PropertyName}' must be empty.");
        });

        RuleFor(input => input.IsEnabled)
            .Equal(true)
            .WithMessage("A required binding must not be disabled, so '{PropertyName}' must be true.")
            .When(input => input.IsRequired);
    }

    /// <summary>The message of a reference that a binding kind requires.</summary>
    private static string KindRequires(string bindingKind)
    {
        return $"'{{PropertyName}}' is required for a {bindingKind} binding.";
    }

    /// <summary>The message of a reference that only one binding kind uses.</summary>
    private static string OnlyKindUses(string bindingKind)
    {
        return $"Only {bindingKind} bindings use '{{PropertyName}}', so it must be empty.";
    }

    /// <summary>The message of the grouping reference a grouping mode requires.</summary>
    private static string ModeRequires(string groupingMode)
    {
        return $"'{{PropertyName}}' is required for {groupingMode} grouping.";
    }

    /// <summary>The message of a grouping reference that only one grouping mode uses.</summary>
    private static string OnlyModeUses(string groupingMode)
    {
        return $"Only {groupingMode} grouping uses '{{PropertyName}}', so it must be empty.";
    }
}
