namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>A new field of a draft dataset version.</summary>
public sealed record FieldInput : FieldUpdateInput
{
    /// <summary>Stable logical field identity, the request-facing grouping ID; fixed once created.</summary>
    public required string FieldKey { get; init; }
}
