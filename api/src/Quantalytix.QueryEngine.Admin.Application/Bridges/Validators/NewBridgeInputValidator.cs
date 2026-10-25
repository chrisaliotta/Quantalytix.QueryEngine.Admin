using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Checks a new bridge: its identity attributes, a unique, well-formed key, and analytical endpoint dimensions that exist. A bridge
/// into a report dimension belongs to a source of that dimension's report, whose editor creates it, and no bridge starts from one.
/// </summary>
public sealed class NewBridgeInputValidator : AbstractValidator<NewBridgeInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="bridges">Reads existing bridge keys.</param>
    /// <param name="dimensions">Reads the endpoint dimensions.</param>
    public NewBridgeInputValidator(ITableGateway<BridgeRecord> bridges, ITableGateway<DimensionRecord> dimensions)
    {
        Include(new BridgeInputValidator());
        RuleFor(input => input.BridgeKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await bridges.ListAsync(new { BridgeKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"A bridge with key '{input.BridgeKey}' already exists.");
        RuleFor(input => input.SourceDimensionId)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (dimensionId, cancellationToken) => await PurposeAsync(dimensions, dimensionId, cancellationToken) is not null)
            .WithMessage(input => $"Dimension {input.SourceDimensionId} does not exist.")
            .MustAsync(async (dimensionId, cancellationToken) => await PurposeAsync(dimensions, dimensionId, cancellationToken) == DimensionPurposes.Analytical)
            .WithMessage(input => $"Dimension {input.SourceDimensionId} holds a report's rows; no bridge starts from a report dimension.");
        RuleFor(input => input.TargetDimensionId)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (dimensionId, cancellationToken) => await PurposeAsync(dimensions, dimensionId, cancellationToken) is not null)
            .WithMessage(input => $"Dimension {input.TargetDimensionId} does not exist.")
            .MustAsync(async (dimensionId, cancellationToken) => await PurposeAsync(dimensions, dimensionId, cancellationToken) == DimensionPurposes.Analytical)
            .WithMessage(input => $"Dimension {input.TargetDimensionId} holds a report's rows; its report's sources create the bridges into it.");
    }

    /// <summary>The purpose of a dimension, or null when it does not exist.</summary>
    private static async Task<string?> PurposeAsync(ITableGateway<DimensionRecord> dimensions, long dimensionId, CancellationToken cancellationToken)
    {
        return (await dimensions.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).SingleOrDefault()?.DimensionPurpose;
    }
}
