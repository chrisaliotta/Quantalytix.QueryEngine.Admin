using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a source profile: a key free among the version's profiles, and one pick per variable, each a source of the version that
/// supplies that variable. Whether the picks cover every variable the report reads is the <c>profiles_complete</c> rule's concern.
/// </summary>
public sealed class SourceProfileInputValidator : AbstractValidator<SourceProfileInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Names the route's report version and edited profile.</param>
    /// <param name="profiles">Reads the version's profile keys.</param>
    /// <param name="sources">Reads the picked sources.</param>
    public SourceProfileInputValidator(VersionKeys keys, ITableGateway<SourceProfileRecord> profiles, ITableGateway<VariableSourceRecord> sources)
    {
        RuleFor(input => input.ProfileKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync((key, cancellationToken) => keys.KeyFreeAsync(profiles, new { ReportVersionId = keys.VersionId, ProfileKey = key },
                profile => profile.SourceProfileId, cancellationToken))
            .WithMessage(input => $"The report version already has a profile with key '{input.ProfileKey}'.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Picks)
            .Must(picks => picks.DistinctBy(pick => pick.VariableKey).Count() == picks.Count)
            .WithMessage("'{PropertyName}' must pick one source per variable.");
        RuleFor(input => input.Picks).CustomAsync(async (picks, context, cancellationToken) =>
        {
            var picked = (await sources.ListAsync(new { VariableSourceId = picks.Select(pick => pick.VariableSourceId).ToArray() }, cancellationToken))
                .ToDictionary(source => source.VariableSourceId);
            foreach (var pick in picks)
            {
                if (!picked.TryGetValue(pick.VariableSourceId, out var source) || source.ReportVersionId != keys.VersionId || source.VariableKey != pick.VariableKey)
                    context.AddFailure($"Source {pick.VariableSourceId} is not a source of variable '{pick.VariableKey}' in this report version.");
            }
        });
    }
}
