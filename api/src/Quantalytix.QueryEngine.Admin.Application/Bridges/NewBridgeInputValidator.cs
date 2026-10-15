using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Checks a new bridge: its identity attributes, a unique, well-formed key, and endpoint dimensions that exist.</summary>
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
            .MustAsync(async (dimensionId, cancellationToken) => await ExistsAsync(dimensions, dimensionId, cancellationToken))
            .WithMessage(input => $"Dimension {input.SourceDimensionId} does not exist.");
        RuleFor(input => input.TargetDimensionId)
            .MustAsync(async (dimensionId, cancellationToken) => await ExistsAsync(dimensions, dimensionId, cancellationToken))
            .WithMessage(input => $"Dimension {input.TargetDimensionId} does not exist.");
    }

    /// <summary>Whether a dimension exists.</summary>
    private static async Task<bool> ExistsAsync(ITableGateway<DimensionRecord> dimensions, long dimensionId, CancellationToken cancellationToken)
    {
        return (await dimensions.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).Count == 1;
    }
}
