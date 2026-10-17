namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A calculation dimension route of a draft model version, as its create and edit endpoints take it.</summary>
public sealed record CalculationRouteInput
{
    /// <summary>The calculation dimension of the version the route produces.</summary>
    public required long CalculationDimensionId { get; init; }

    /// <summary>The dataset the route reads.</summary>
    public required long DatasetId { get; init; }

    /// <summary>A source of the version, over the same dataset, whose route this overrides; null for the dataset's default route.</summary>
    public long? VariableSourceId { get; init; }

    /// <summary>A <c>route_kind</c> value: <c>semantic</c> for a canonical dimension, <c>field</c> for a scalar one.</summary>
    public required string RouteKind { get; init; }

    /// <summary>The dataset field that produces the dimension.</summary>
    public required string FieldKey { get; init; }
}
