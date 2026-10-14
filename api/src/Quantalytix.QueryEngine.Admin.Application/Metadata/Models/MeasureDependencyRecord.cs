namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_measure_dependency</c> row: one edge of the derived dependency graph of stored formula measures.</summary>
public sealed record MeasureDependencyRecord
{
    /// <summary>The formula measure that has the dependency; first part of the primary key.</summary>
    public required long MeasureId { get; init; }

    /// <summary>The measure required to evaluate it; second part of the primary key.</summary>
    public required long DependencyMeasureId { get; init; }
}
