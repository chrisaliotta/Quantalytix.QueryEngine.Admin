using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Checks an edit of a report version: its JSON shapes and subtotal position, and a default profile of the version's own.</summary>
public sealed class ReportVersionInputValidator : AbstractValidator<ReportVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="profiles">Reads the default profile.</param>
    /// <param name="route">Names the edited version.</param>
    public ReportVersionInputValidator(ITableGateway<SourceProfileRecord> profiles, IRequestRoute route)
    {
        Include(new ReportVersionShape());
        RuleFor(input => input.DefaultProfileKey)
            .MustAsync(async (profileKey, cancellationToken) =>
                (await profiles.ListAsync(new { ReportVersionId = route.Id("versionId"), ProfileKey = profileKey }, cancellationToken)).Count == 1)
            .WithMessage(input => $"'{input.DefaultProfileKey}' is no profile of this report version.")
            .When(input => input.DefaultProfileKey is not null);
    }
}
