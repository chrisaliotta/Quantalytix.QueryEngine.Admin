using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a new report: its identity attributes, a unique, well-formed key that no dimension has either, because the report's
/// own dimension takes it, and a calculation model with a published version, which the report reads from its creation on. A
/// dimension the report starts from must be categorical or hierarchical with a published version of one root, because a report's
/// rows are one tree.
/// </summary>
public sealed class NewReportInputValidator : AbstractValidator<NewReportInput>
{
    /// <summary>The dimension kinds whose members a report's rows can start as a copy of.</summary>
    private static readonly string[] TemplateKinds = [DimensionKinds.Categorical, DimensionKinds.Hierarchical];

    /// <summary>Declares the rules.</summary>
    /// <param name="reports">Reads existing report keys.</param>
    /// <param name="dimensions">Reads existing dimension keys and the template dimension.</param>
    /// <param name="modelVersions">Reads the model's published version.</param>
    /// <param name="publishedDimensions">Reads the template's published version.</param>
    public NewReportInputValidator(
        ITableGateway<ReportRecord> reports,
        ITableGateway<DimensionRecord> dimensions,
        ITableGateway<CalculationModelVersionRecord> modelVersions,
        PublishedDimensionReader publishedDimensions)
    {
        Include(new ReportInputValidator());
        RuleFor(input => input.ReportKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await reports.ListAsync(new { ReportKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"A report with key '{input.ReportKey}' already exists.")
            .MustAsync(async (key, cancellationToken) => (await dimensions.ListAsync(new { DimensionKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"The report's own dimension takes its key, and a dimension with key '{input.ReportKey}' already exists.");
        RuleFor(input => input.ModelId)
            .MustAsync(async (modelId, cancellationToken) =>
                (await modelVersions.ListAsync(new { ModelId = modelId, Status = PublicationStatuses.Published }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Calculation model {input.ModelId} has no published version, which a report reads.");
        RuleFor(input => input.StartFromDimensionId)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (dimensionId, cancellationToken) =>
                (await dimensions.ListAsync(new { DimensionId = dimensionId, DimensionKind = TemplateKinds }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dimension {input.StartFromDimensionId} is no categorical or hierarchical dimension.")
            .MustAsync(async (dimensionId, cancellationToken) => (await publishedDimensions.ReadAsync(dimensionId!.Value, cancellationToken))?.RootCount == 1)
            .WithMessage(input => $"Dimension {input.StartFromDimensionId} has no published version with one root, which a report's rows need.")
            .When(input => input.StartFromDimensionId is not null);
    }
}
