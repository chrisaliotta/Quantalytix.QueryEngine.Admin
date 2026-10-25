using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a complete scope list: one scope per variable, each with a well-formed variable key, a member key and a selection that
/// reads a member. Whether the model has the variable and the report's rows the member is the report rules' concern, because the
/// model and the rows change too.
/// </summary>
public sealed class ReportVariableScopesInputValidator : AbstractValidator<ReportVariableScopesInput>
{
    /// <summary>Declares the rules.</summary>
    public ReportVariableScopesInputValidator()
    {
        RuleForEach(input => input.Scopes).ChildRules(scope =>
        {
            scope.RuleFor(input => input.VariableKey).MustBeKey(128);
            scope.RuleFor(input => input.ScopeMemberKey).NotEmpty().MustFitVarchar(128);
            scope.RuleFor(input => input.SelectionScope)
                .MustBeIn(SelectionScopes.Definition)
                .NotEqual(SelectionScopes.AllMapped)
                .WithMessage("A variable without a scope reads every row it classifies, so a scope's '{PropertyName}' is exact or descendants.");
        });
        RuleFor(input => input.Scopes)
            .Must(list => list.DistinctBy(scope => scope.VariableKey).Count() == list.Count)
            .WithMessage("'{PropertyName}' must scope each variable once.");
    }
}
