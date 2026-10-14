namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>A new measure of a draft dataset version.</summary>
public sealed record MeasureInput : MeasureUpdateInput
{
    /// <summary>The request-facing measure ID, unique within the version; fixed once created.</summary>
    public required string MeasureKey { get; init; }
}
