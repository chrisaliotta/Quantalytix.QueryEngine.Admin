namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A model parameter route: the calculation model and the key of one of its parameters, which a write creates when it is new.</summary>
public sealed record ModelParameterReference
{
    /// <summary>The model that declares the parameter.</summary>
    public required long ModelId { get; init; }

    /// <summary>The parameter's key.</summary>
    public required string ParameterKey { get; init; }
}
