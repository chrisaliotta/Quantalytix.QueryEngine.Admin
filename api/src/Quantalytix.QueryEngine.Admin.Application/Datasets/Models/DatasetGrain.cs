namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetGrain</c>.</summary>
public sealed record DatasetGrain : DatasetGrainDefinition
{
    /// <summary>The grain's id, stored or temporary.</summary>
    public required string Id { get; init; }
}
