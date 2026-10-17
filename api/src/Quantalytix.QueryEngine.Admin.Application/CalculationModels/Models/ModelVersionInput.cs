namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The editable attributes of a draft model version, as <c>PUT model-versions/{versionId}</c> takes them.</summary>
public record ModelVersionInput
{
    /// <summary>The ISO 4217 currency of the model's monetary figures, such as <c>USD</c>.</summary>
    public required string CurrencyCode { get; init; }
}
