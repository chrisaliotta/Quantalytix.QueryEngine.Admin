using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a new report version: its JSON shapes and subtotal position, a calculation model that exists, and a clone source that is a
/// version of the route's report. The default profile is the <c>default_profile_enabled</c> rule's concern, because a new version
/// has no profiles yet.
/// </summary>
public sealed class NewReportVersionInputValidator : AbstractValidator<NewReportVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="models">Reads the calculation model.</param>
    /// <param name="versions">Reads the clone source.</param>
    /// <param name="route">Names the report the new version belongs to.</param>
    public NewReportVersionInputValidator(
        ITableGateway<CalculationModelRecord> models,
        ITableGateway<ReportVersionRecord> versions,
        IRequestRoute route)
    {
        Include(new ReportVersionShape());
        RuleFor(input => input.ModelId)
            .MustAsync(async (modelId, cancellationToken) => (await models.ListAsync(new { ModelId = modelId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Calculation model {input.ModelId} does not exist.");
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await versions.ListAsync(new { ReportVersionId = versionId, ReportId = route.Id("reportId") }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Report version {input.CloneFromVersionId} is not a version of this report.")
            .When(input => input.CloneFromVersionId is not null);
    }
}
