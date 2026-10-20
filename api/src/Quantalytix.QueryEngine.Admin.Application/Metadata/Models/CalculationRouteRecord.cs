namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_calculation_dimension_binding</c> row: how one dataset, or one source, produces a calculation dimension: a raw field,
/// or a classification into the calculation dimension's dimension.
/// </summary>
public sealed record CalculationRouteRecord
{
    /// <summary>Primary key.</summary>
    public required long CalculationBindingId { get; init; }

    /// <summary>The owning report version.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>The calculation dimension.</summary>
    public required long CalculationDimensionId { get; init; }

    /// <summary>The dataset the route reads.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The source the route overrides the dataset's default route for; null for the default.</summary>
    public long? VariableSourceId { get; init; }

    /// <summary>A <c>route_kind</c> value.</summary>
    public required string RouteKind { get; init; }

    /// <summary>The dataset field the route reads.</summary>
    public required string FieldKey { get; init; }

    /// <summary>The mapping set of a <c>mapped</c> route, into the calculation dimension's dimension.</summary>
    public long? MappingSetId { get; init; }

    /// <summary>The bridge of a <c>bridge</c> route, into the calculation dimension's dimension.</summary>
    public long? BridgeId { get; init; }
}
