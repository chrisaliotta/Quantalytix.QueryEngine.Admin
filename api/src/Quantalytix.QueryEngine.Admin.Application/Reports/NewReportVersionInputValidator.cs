using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a new report version: its JSON shapes, a report dimension and calculation model that exist, a default profile of the
/// model's published version, and a clone source that is a version of the route's report.
/// </summary>
public sealed class NewReportVersionInputValidator : AbstractValidator<NewReportVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="dimensions">Reads the report dimension.</param>
    /// <param name="models">Reads the calculation model.</param>
    /// <param name="metrics">Finds the model's published version.</param>
    /// <param name="profiles">Reads the default profile.</param>
    /// <param name="versions">Reads the clone source.</param>
    /// <param name="route">Names the report the new version belongs to.</param>
    public NewReportVersionInputValidator(
        ITableGateway<DimensionRecord> dimensions,
        ITableGateway<CalculationModelRecord> models,
        ModelMetricsReader metrics,
        ITableGateway<SourceProfileRecord> profiles,
        ITableGateway<ReportVersionRecord> versions,
        IRequestRoute route)
    {
        Include(new ReportVersionShape());
        RuleFor(input => input.ReportDimensionId)
            .MustAsync(async (dimensionId, cancellationToken) => (await dimensions.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dimension {input.ReportDimensionId} does not exist.");
        RuleFor(input => input.ModelId)
            .MustAsync(async (modelId, cancellationToken) => (await models.ListAsync(new { ModelId = modelId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Calculation model {input.ModelId} does not exist.");
        RuleFor(input => input.DefaultProfileKey)
            .MustAsync(async (input, profileKey, cancellationToken) =>
                await metrics.PublishedVersionAsync(input.ModelId, cancellationToken) is { } model
                && (await profiles.ListAsync(new { model.ModelVersionId, ProfileKey = profileKey }, cancellationToken)).Count == 1)
            .WithMessage(input => $"'{input.DefaultProfileKey}' is no profile of the published version of calculation model {input.ModelId}.")
            .When(input => input.DefaultProfileKey is not null);
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await versions.ListAsync(new { ReportVersionId = versionId, ReportId = route.Id("reportId") }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Report version {input.CloneFromVersionId} is not a version of this report.")
            .When(input => input.CloneFromVersionId is not null);
    }
}
