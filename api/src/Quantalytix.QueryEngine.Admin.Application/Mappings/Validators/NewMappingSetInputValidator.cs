using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks a new mapping set: its identity attributes, a unique, well-formed key, and an analytical target dimension that exists.
/// A report dimension's rows are reached through its report's sources only, by bridge or by field, so no mapping set targets one.
/// </summary>
public sealed class NewMappingSetInputValidator : AbstractValidator<NewMappingSetInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="mappingSets">Reads existing mapping set keys.</param>
    /// <param name="dimensions">Reads the target dimension.</param>
    public NewMappingSetInputValidator(ITableGateway<MappingSetRecord> mappingSets, ITableGateway<DimensionRecord> dimensions)
    {
        Include(new MappingSetInputValidator());
        RuleFor(input => input.MappingSetKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await mappingSets.ListAsync(new { MappingSetKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"A mapping set with key '{input.MappingSetKey}' already exists.");
        RuleFor(input => input.DimensionId)
            .Cascade(CascadeMode.Stop)
            .MustAsync(async (dimensionId, cancellationToken) => await PurposeAsync(dimensions, dimensionId, cancellationToken) is not null)
            .WithMessage(input => $"Dimension {input.DimensionId} does not exist.")
            .MustAsync(async (dimensionId, cancellationToken) => await PurposeAsync(dimensions, dimensionId, cancellationToken) == DimensionPurposes.Analytical)
            .WithMessage(input => $"Dimension {input.DimensionId} holds a report's rows; its report's sources map into it by bridge or by field.");
    }

    /// <summary>The purpose of a dimension, or null when it does not exist.</summary>
    private static async Task<string?> PurposeAsync(ITableGateway<DimensionRecord> dimensions, long dimensionId, CancellationToken cancellationToken)
    {
        return (await dimensions.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).SingleOrDefault()?.DimensionPurpose;
    }
}
