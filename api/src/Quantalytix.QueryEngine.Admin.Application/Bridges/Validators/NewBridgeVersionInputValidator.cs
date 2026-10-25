using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Checks a new bridge version: its attributes, and that the version it clones is a version of the route's bridge.</summary>
public sealed class NewBridgeVersionInputValidator : AbstractValidator<NewBridgeVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="bridgeVersions">Reads the clone source.</param>
    /// <param name="route">Names the bridge the new version belongs to.</param>
    public NewBridgeVersionInputValidator(ITableGateway<BridgeVersionRecord> bridgeVersions, IRequestRoute route)
    {
        Include(new BridgeVersionInputValidator());
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await bridgeVersions.ListAsync(new { BridgeVersionId = versionId, BridgeId = route.Id("bridgeId") }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Bridge version {input.CloneFromVersionId} is not a version of this bridge.")
            .When(input => input.CloneFromVersionId is not null);
    }
}
