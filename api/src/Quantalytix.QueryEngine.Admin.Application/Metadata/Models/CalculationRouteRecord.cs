namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_dimension_binding</c> row: the field that produces a calculation dimension in one dataset, or for one source.</summary>
public sealed record CalculationRouteRecord
{
    /// <summary>Primary key.</summary>
    public required long CalculationBindingId { get; init; }

    /// <summary>The owning model version.</summary>
    public required long ModelVersionId { get; init; }

    /// <summary>The calculation dimension.</summary>
    public required long CalculationDimensionId { get; init; }

    /// <summary>The dataset the route reads.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The source the route overrides the dataset's default route for; null for the default.</summary>
    public long? VariableSourceId { get; init; }

    /// <summary>A <c>route_kind</c> value.</summary>
    public required string RouteKind { get; init; }

    /// <summary>The dataset field that produces the dimension.</summary>
    public required string FieldKey { get; init; }
}
