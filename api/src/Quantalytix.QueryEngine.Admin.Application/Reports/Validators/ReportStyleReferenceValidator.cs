using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Checks the key a style write names, which rows name the style by.</summary>
public sealed class ReportStyleReferenceValidator : AbstractValidator<ReportStyleReference>
{
    /// <summary>Declares the rules.</summary>
    public ReportStyleReferenceValidator()
    {
        RuleFor(reference => reference.StyleKey).Cascade(CascadeMode.Stop).MustBeKey(128);
    }
}
