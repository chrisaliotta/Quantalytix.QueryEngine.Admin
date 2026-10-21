using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a new report version: its JSON shapes and subtotal position, a calculation model that exists, a report that has no draft
/// yet, and a clone source that is a version of the route's report. A report has one draft at a time, because its draft pairs with
/// the one draft of its report dimension and of each of its bridges. The default profile is the <c>default_profile_enabled</c>
/// rule's concern, because a new version has no profiles yet.
/// </summary>
public sealed class NewReportVersionInputValidator : AbstractValidator<NewReportVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="models">Reads the calculation model.</param>
    /// <param name="versions">Reads the report's drafts and the clone source.</param>
    /// <param name="route">Names the report the new version belongs to.</param>
    public NewReportVersionInputValidator(
        ITableGateway<CalculationModelRecord> models,
        ITableGateway<ReportVersionRecord> versions,
        IRequestRoute route)
    {
        Include(new ReportVersionShape());
        RuleFor(input => input.ModelId)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (modelId, cancellationToken) => (await models.ListAsync(new { ModelId = modelId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Calculation model {input.ModelId} does not exist.")
            .MustAsync(async (_, cancellationToken) =>
                (await versions.ListAsync(new { ReportId = route.Id("reportId"), Status = PublicationStatuses.Draft }, cancellationToken)).Count == 0)
            .WithMessage("The report already has a draft; edit, publish or delete it before creating another.");
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await versions.ListAsync(new { ReportVersionId = versionId, ReportId = route.Id("reportId") }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Report version {input.CloneFromVersionId} is not a version of this report.")
            .When(input => input.CloneFromVersionId is not null);
    }
}
