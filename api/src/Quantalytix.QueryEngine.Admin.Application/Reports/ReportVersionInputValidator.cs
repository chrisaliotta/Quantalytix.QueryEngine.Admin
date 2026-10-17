using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Checks an edit of a draft report version: its JSON shapes, and a default profile of the model version it reads.</summary>
public sealed class ReportVersionInputValidator : AbstractValidator<ReportVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="versions">Reads the edited version's model version.</param>
    /// <param name="profiles">Reads the default profile.</param>
    /// <param name="route">Names the edited version.</param>
    public ReportVersionInputValidator(ITableGateway<ReportVersionRecord> versions, ITableGateway<SourceProfileRecord> profiles, IRequestRoute route)
    {
        Include(new ReportVersionShape());
        RuleFor(input => input.DefaultSourceProfileId)
            .MustAsync(async (profileId, cancellationToken) =>
            {
                var version = await versions.GetAsync(new { ReportVersionId = route.Id("versionId") }, cancellationToken);
                return (await profiles.ListAsync(new { SourceProfileId = profileId, version.ModelVersionId }, cancellationToken)).Count == 1;
            })
            .WithMessage(input => $"Profile {input.DefaultSourceProfileId} is not a profile of the model version this report version reads.")
            .When(input => input.DefaultSourceProfileId is not null);
    }
}
