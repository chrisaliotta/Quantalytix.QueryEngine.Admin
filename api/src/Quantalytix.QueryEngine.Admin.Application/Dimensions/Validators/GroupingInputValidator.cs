using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a grouping: a known mode with exactly the key it needs, a level key for <c>level</c>, an ancestor member key for
/// <c>ancestor</c> and a depth for <c>depth</c>. Whether the keys name a level or member of the dimension's published version is a
/// publication rule, because publishing another dimension version can change the answer.
/// </summary>
/// <typeparam name="T">The input that groups members.</typeparam>
public sealed class GroupingInputValidator<T> : AbstractValidator<T>
    where T : IGroupingInput
{
    /// <summary>Declares the rules.</summary>
    /// <param name="ancestorKeyLength">The length of the column that stores the ancestor member key.</param>
    public GroupingInputValidator(int ancestorKeyLength)
    {
        RuleFor(input => input.GroupingMode).NotNull().MustBeIn(GroupingModes.Definition);
        When(input => input.GroupingMode == GroupingModes.Level, () =>
        {
            RuleFor(input => input.GroupingLevelKey).NotEmpty().WithMessage(ModeRequires(GroupingModes.Level)).MustFitVarchar(128);
        }).Otherwise(() =>
        {
            RuleFor(input => input.GroupingLevelKey).Null().WithMessage(OnlyModeUses(GroupingModes.Level));
        });
        When(input => input.GroupingMode == GroupingModes.Ancestor, () =>
        {
            RuleFor(input => input.GroupingAncestorMemberKey).NotEmpty().WithMessage(ModeRequires(GroupingModes.Ancestor)).MustFitVarchar(ancestorKeyLength);
        }).Otherwise(() =>
        {
            RuleFor(input => input.GroupingAncestorMemberKey).Null().WithMessage(OnlyModeUses(GroupingModes.Ancestor));
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
