namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>The contract's <c>DatasetBinding</c>.</summary>
public sealed record DatasetBinding : DatasetBindingDefinition
{
    /// <summary>The binding's id, stored or temporary.</summary>
    public required string Id { get; init; }
}
