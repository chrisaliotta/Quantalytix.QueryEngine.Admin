using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a source profile: a key free among the report's profiles, and one pick per variable, each a source of the report that
/// supplies that variable. Whether the picks cover every variable the report reads is the <c>profiles_complete</c> rule's concern.
/// </summary>
public sealed class SourceProfileInputValidator : AbstractValidator<SourceProfileInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Names the route's report and edited profile.</param>
    /// <param name="profiles">Reads the report's profile keys.</param>
    /// <param name="sources">Reads the report's sources.</param>
    /// <param name="sourceVariables">Reads the variables the sources supply.</param>
    public SourceProfileInputValidator(
        ContentKeys keys,
        ITableGateway<SourceProfileRecord> profiles,
        ITableGateway<ReportSourceRecord> sources,
        ITableGateway<ReportSourceVariableRecord> sourceVariables)
    {
        RuleFor(input => input.ProfileKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync((key, cancellationToken) => keys.KeyFreeAsync(profiles, new { keys.ReportId, ProfileKey = key },
                profile => profile.SourceProfileId, cancellationToken))
            .WithMessage(input => $"The report already has a profile with key '{input.ProfileKey}'.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Picks)
            .Must(picks => picks.DistinctBy(pick => pick.VariableKey).Count() == picks.Count)
            .WithMessage("'{PropertyName}' must pick one source per variable.");
        RuleFor(input => input.Picks).CustomAsync(async (picks, context, cancellationToken) =>
        {
            long[] reportSources = [.. (await sources.ListAsync(new { keys.ReportId }, cancellationToken)).Select(source => source.ReportSourceId)];
            var supplied = (await sourceVariables.ListAsync(new { ReportSourceId = reportSources }, cancellationToken))
                .Select(variable => (variable.ReportSourceId, variable.VariableKey))
                .ToHashSet();
            foreach (var pick in picks.Where(pick => !supplied.Contains((pick.ReportSourceId, pick.VariableKey))))
                context.AddFailure($"Source {pick.ReportSourceId} is no source of this report that supplies variable '{pick.VariableKey}'.");
        });
    }
}
