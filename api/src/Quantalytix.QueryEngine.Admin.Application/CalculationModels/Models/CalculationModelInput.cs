namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The editable identity attributes of a calculation model, as <c>PUT calculation-models/{modelId}</c> takes them.</summary>
public record CalculationModelInput
{
    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
