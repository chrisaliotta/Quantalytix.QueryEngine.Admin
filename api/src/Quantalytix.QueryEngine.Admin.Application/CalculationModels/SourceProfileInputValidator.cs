using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a source profile: a key free among the version's profiles, and picks that each name a variable of the version once,
/// with one of that variable's sources. That every variable has a pick is the <c>profiles_complete</c> publication rule's
/// concern, because variables can be added after the profile.
/// </summary>
public sealed class SourceProfileInputValidator : AbstractValidator<SourceProfileInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Checks the key against the route's model version.</param>
    /// <param name="profiles">Reads the version's profile keys.</param>
    /// <param name="sources">Reads the picked sources.</param>
    public SourceProfileInputValidator(ModelKeys keys, ITableGateway<SourceProfileRecord> profiles, ITableGateway<VariableSourceRecord> sources)
    {
        RuleFor(input => input.ProfileKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync((key, cancellationToken) => keys.KeyFreeAsync(profiles, new { ModelVersionId = keys.VersionId, ProfileKey = key },
                profile => profile.SourceProfileId, cancellationToken))
            .WithMessage(input => $"The model version already has a profile with key '{input.ProfileKey}'.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Picks)
            .Must(picks => picks.DistinctBy(pick => pick.VariableId).Count() == picks.Count)
            .WithMessage("'{PropertyName}' must pick one source per variable.");
        RuleFor(input => input.Picks).CustomAsync(async (picks, context, cancellationToken) =>
        {
            var picked = (await sources.ListAsync(new { VariableSourceId = picks.Select(pick => pick.VariableSourceId).ToArray() }, cancellationToken))
                .ToDictionary(source => source.VariableSourceId);
            foreach (var pick in picks)
            {
                if (!picked.TryGetValue(pick.VariableSourceId, out var source) || source.ModelVersionId != keys.VersionId || source.VariableId != pick.VariableId)
                    context.AddFailure($"Source {pick.VariableSourceId} is not a source of variable {pick.VariableId} in this model version.");
            }
        });
    }
}
