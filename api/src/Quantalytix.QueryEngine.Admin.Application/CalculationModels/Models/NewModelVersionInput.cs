namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// A new draft version of a calculation model, as <c>POST calculation-models/{modelId}/versions</c> takes it: its attributes,
/// and optionally a version of the same model whose content it copies.
/// </summary>
public sealed record NewModelVersionInput : ModelVersionInput
{
    /// <summary>A version of the same model whose variables and calculated measures the draft copies; null for an empty draft.</summary>
    public long? CloneFromVersionId { get; init; }
}
