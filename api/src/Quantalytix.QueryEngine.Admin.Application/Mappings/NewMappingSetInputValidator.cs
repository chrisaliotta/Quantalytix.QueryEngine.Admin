using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Checks a new mapping set: its identity attributes, a unique, well-formed key, and a target dimension that exists.</summary>
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
            .MustAsync(async (dimensionId, cancellationToken) =>
                (await dimensions.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dimension {input.DimensionId} does not exist.");
    }
}
