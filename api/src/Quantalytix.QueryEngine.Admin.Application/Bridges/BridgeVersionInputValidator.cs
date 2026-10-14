using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Checks a bridge version's attributes: endpoint versions that exist, and known cardinality and allocation values. That the
/// endpoints belong to the bridge's dimensions needs the route too, so the <c>endpoint_versions_match_bridge</c> rule checks
/// it; <see cref="NewBridgeVersionInputValidator"/> includes these rules.
/// </summary>
public sealed class BridgeVersionInputValidator : AbstractValidator<BridgeVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="dimensionVersions">Reads the endpoint versions.</param>
    public BridgeVersionInputValidator(ITableGateway<DimensionVersionRecord> dimensionVersions)
    {
        RuleFor(input => input.SourceDimensionVersionId)
            .MustAsync(async (versionId, cancellationToken) => await ExistsAsync(dimensionVersions, versionId, cancellationToken))
            .WithMessage(input => $"Dimension version {input.SourceDimensionVersionId} does not exist.");
        RuleFor(input => input.TargetDimensionVersionId)
            .MustAsync(async (versionId, cancellationToken) => await ExistsAsync(dimensionVersions, versionId, cancellationToken))
            .WithMessage(input => $"Dimension version {input.TargetDimensionVersionId} does not exist.");
        RuleFor(input => input.Cardinality).MustBeIn(Cardinalities.Definition);
        RuleFor(input => input.AllocationMode).MustBeIn(AllocationModes.Definition);
    }

    /// <summary>Whether a dimension version exists.</summary>
    private static async Task<bool> ExistsAsync(ITableGateway<DimensionVersionRecord> dimensionVersions, long versionId, CancellationToken cancellationToken)
    {
        return (await dimensionVersions.ListAsync(new { DimensionVersionId = versionId }, cancellationToken)).Count == 1;
    }
}
