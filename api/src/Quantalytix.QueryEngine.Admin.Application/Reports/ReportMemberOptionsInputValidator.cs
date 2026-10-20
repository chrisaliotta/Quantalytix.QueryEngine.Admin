using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a complete member option list: one option per member, each with a member key and an optional label. Whether the report's
/// dimension has the member is a publication rule, because the answer changes with its published version.
/// </summary>
public sealed class ReportMemberOptionsInputValidator : AbstractValidator<ReportMemberOptionsInput>
{
    /// <summary>Declares the rules.</summary>
    public ReportMemberOptionsInputValidator()
    {
        RuleForEach(input => input.Options).ChildRules(option =>
        {
            option.RuleFor(input => input.MemberKey).NotEmpty().MustFitVarchar(128);
            option.RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256).When(input => input.DisplayName is not null);
        });
        RuleFor(input => input.Options)
            .Must(list => list.DistinctBy(option => option.MemberKey).Count() == list.Count)
            .WithMessage("'{PropertyName}' must give each member one option.");
    }
}
