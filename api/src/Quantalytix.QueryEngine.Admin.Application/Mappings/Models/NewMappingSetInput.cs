namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// A new mapping set, as <c>POST mapping-sets</c> takes it: its fixed key and target dimension, plus its identity attributes.
/// </summary>
public sealed record NewMappingSetInput : MappingSetInput
{
    /// <summary>Stable key, unique across mapping sets and fixed once created.</summary>
    public required string MappingSetKey { get; init; }

    /// <summary>The canonical dimension the source codes map onto; fixed once created.</summary>
    public required long DimensionId { get; init; }
}
