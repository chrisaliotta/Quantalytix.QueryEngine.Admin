using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a complete target list: one target per variable, each with a well-formed variable key, a member key and a scope that reads
/// a target. Whether the model has the variable and the report's dimension the member is a publication rule, because both answers
/// change with the published versions.
/// </summary>
public sealed class ReportVariableTargetsInputValidator : AbstractValidator<ReportVariableTargetsInput>
{
    /// <summary>Declares the rules.</summary>
    public ReportVariableTargetsInputValidator()
    {
        RuleForEach(input => input.Targets).ChildRules(target =>
        {
            target.RuleFor(input => input.VariableKey).MustBeKey(128);
            target.RuleFor(input => input.TargetMemberKey).NotEmpty().MustFitVarchar(128);
            target.RuleFor(input => input.SelectionScope)
                .MustBeIn(SelectionScopes.Definition)
                .NotEqual(SelectionScopes.AllMapped)
                .WithMessage("A variable without a target reads every row it classifies, so a target's '{PropertyName}' is exact or descendants.");
        });
        RuleFor(input => input.Targets)
            .Must(list => list.DistinctBy(target => target.VariableKey).Count() == list.Count)
            .WithMessage("'{PropertyName}' must target each variable once.");
    }
}
