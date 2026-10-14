namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>A request to pin, for each dimension, the published version effective at the assumption set's <c>effective_at</c>.</summary>
public sealed record AssumptionCaptureInput
{
    /// <summary>The dimensions to capture.</summary>
    public required IReadOnlyList<long> DimensionIds { get; init; }
}
