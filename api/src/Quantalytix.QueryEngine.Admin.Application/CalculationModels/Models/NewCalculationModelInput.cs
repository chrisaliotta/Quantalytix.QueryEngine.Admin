namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A new calculation model, as <c>POST calculation-models</c> takes it: its fixed key plus its identity attributes.</summary>
public sealed record NewCalculationModelInput : CalculationModelInput
{
    /// <summary>Stable key, unique across calculation models and fixed once created.</summary>
    public required string ModelKey { get; init; }
}
