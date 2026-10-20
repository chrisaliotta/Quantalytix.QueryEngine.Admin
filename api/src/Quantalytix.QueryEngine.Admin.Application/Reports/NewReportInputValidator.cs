using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Checks a new report: its identity attributes, a unique, well-formed key and a report dimension that exists.</summary>
public sealed class NewReportInputValidator : AbstractValidator<NewReportInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="reports">Reads existing report keys.</param>
    /// <param name="dimensions">Reads the report dimension.</param>
    public NewReportInputValidator(ITableGateway<ReportRecord> reports, ITableGateway<DimensionRecord> dimensions)
    {
        Include(new ReportInputValidator());
        RuleFor(input => input.ReportKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await reports.ListAsync(new { ReportKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"A report with key '{input.ReportKey}' already exists.");
        RuleFor(input => input.ReportDimensionId)
            .MustAsync(async (dimensionId, cancellationToken) => (await dimensions.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dimension {input.ReportDimensionId} does not exist.");
    }
}
