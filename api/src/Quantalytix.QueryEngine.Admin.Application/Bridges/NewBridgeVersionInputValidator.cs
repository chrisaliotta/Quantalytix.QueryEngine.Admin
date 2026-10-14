using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Checks a new bridge version: its attributes, and that the version it clones exists.</summary>
public sealed class NewBridgeVersionInputValidator : AbstractValidator<NewBridgeVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="dimensionVersions">Reads the endpoint versions.</param>
    /// <param name="bridgeVersions">Reads the clone source.</param>
    public NewBridgeVersionInputValidator(
        ITableGateway<DimensionVersionRecord> dimensionVersions,
        ITableGateway<BridgeVersionRecord> bridgeVersions)
    {
        Include(new BridgeVersionInputValidator(dimensionVersions));
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await bridgeVersions.ListAsync(new { BridgeVersionId = versionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Bridge version {input.CloneFromVersionId} does not exist.")
            .When(input => input.CloneFromVersionId is not null);
    }
}
