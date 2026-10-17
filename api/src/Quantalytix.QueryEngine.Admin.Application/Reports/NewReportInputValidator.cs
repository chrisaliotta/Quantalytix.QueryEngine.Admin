using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Checks a new report: its identity attributes and a unique, well-formed key.</summary>
public sealed class NewReportInputValidator : AbstractValidator<NewReportInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="reports">Reads existing report keys.</param>
    public NewReportInputValidator(ITableGateway<ReportRecord> reports)
    {
        Include(new ReportInputValidator());
        RuleFor(input => input.ReportKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await reports.ListAsync(new { ReportKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"A report with key '{input.ReportKey}' already exists.");
    }
}
