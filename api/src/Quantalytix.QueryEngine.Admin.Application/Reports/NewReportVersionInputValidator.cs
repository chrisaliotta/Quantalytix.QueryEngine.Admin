using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a new report version: its JSON shapes, a report dimension and model version that exist, a default profile of that
/// model version, and a clone source that is a version of the route's report.
/// </summary>
public sealed class NewReportVersionInputValidator : AbstractValidator<NewReportVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="dimensions">Reads the report dimension.</param>
    /// <param name="modelVersions">Reads the model version.</param>
    /// <param name="profiles">Reads the default profile.</param>
    /// <param name="versions">Reads the clone source.</param>
    /// <param name="route">Names the report the new version belongs to.</param>
    public NewReportVersionInputValidator(
        ITableGateway<DimensionRecord> dimensions,
        ITableGateway<CalculationModelVersionRecord> modelVersions,
        ITableGateway<SourceProfileRecord> profiles,
        ITableGateway<ReportVersionRecord> versions,
        IRequestRoute route)
    {
        Include(new ReportVersionShape());
        RuleFor(input => input.ReportDimensionId)
            .MustAsync(async (dimensionId, cancellationToken) => (await dimensions.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dimension {input.ReportDimensionId} does not exist.");
        RuleFor(input => input.ModelVersionId)
            .MustAsync(async (versionId, cancellationToken) => (await modelVersions.ListAsync(new { ModelVersionId = versionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Model version {input.ModelVersionId} does not exist.");
        RuleFor(input => input.DefaultSourceProfileId)
            .MustAsync(async (input, profileId, cancellationToken) =>
                (await profiles.ListAsync(new { SourceProfileId = profileId, input.ModelVersionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Profile {input.DefaultSourceProfileId} is not a profile of model version {input.ModelVersionId}.")
            .When(input => input.DefaultSourceProfileId is not null);
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await versions.ListAsync(new { ReportVersionId = versionId, ReportId = route.Id("reportId") }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Report version {input.CloneFromVersionId} is not a version of this report.")
            .When(input => input.CloneFromVersionId is not null);
    }
}
