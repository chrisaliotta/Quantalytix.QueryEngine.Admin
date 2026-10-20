namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A calculation dimension route of an editable report version, as its create and edit endpoints take it.</summary>
public sealed record CalculationRouteInput
{
    /// <summary>The calculation dimension of the version the route produces.</summary>
    public required long CalculationDimensionId { get; init; }

    /// <summary>The dataset the route reads.</summary>
    public required long DatasetId { get; init; }

    /// <summary>A source of the version, over the same dataset, whose route this overrides; null for the dataset's default route.</summary>
    public long? VariableSourceId { get; init; }

    /// <summary>How the route produces its dimension: <c>field</c> for a scalar calculation dimension, otherwise a classification into the canonical one.</summary>
    public required ClassificationInput Classification { get; init; }
}
