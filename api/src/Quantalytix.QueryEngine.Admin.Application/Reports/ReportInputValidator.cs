using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Checks the identity attributes of a report; <see cref="NewReportInputValidator"/> includes these rules.</summary>
public sealed class ReportInputValidator : AbstractValidator<ReportInput>
{
    /// <summary>Declares the rules.</summary>
    public ReportInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.ReportType).NotEmpty().MustFitVarchar(32);
        RuleFor(input => input.PayloadJson).MustBeJson();
    }
}
