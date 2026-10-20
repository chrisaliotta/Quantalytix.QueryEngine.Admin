using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Checks an edit of a report version: its JSON shapes, and a default profile of its model's published version.</summary>
public sealed class ReportVersionInputValidator : AbstractValidator<ReportVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="metrics">Finds the published version of the edited version's model.</param>
    /// <param name="profiles">Reads the default profile.</param>
    /// <param name="route">Names the edited version.</param>
    public ReportVersionInputValidator(ModelMetricsReader metrics, ITableGateway<SourceProfileRecord> profiles, IRequestRoute route)
    {
        Include(new ReportVersionShape());
        RuleFor(input => input.DefaultProfileKey)
            .MustAsync(async (profileKey, cancellationToken) =>
                await metrics.ForReportAsync(route.Id("versionId"), cancellationToken) is { } model
                && (await profiles.ListAsync(new { model.ModelVersionId, ProfileKey = profileKey }, cancellationToken)).Count == 1)
            .WithMessage(input => $"'{input.DefaultProfileKey}' is no profile of the published version of the report's model.")
            .When(input => input.DefaultProfileKey is not null);
    }
}
