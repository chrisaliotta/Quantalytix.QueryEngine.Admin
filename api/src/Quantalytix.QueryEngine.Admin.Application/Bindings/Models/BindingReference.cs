namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>Identifies a dimension binding of a dataset version from the route; the binding must belong to the version.</summary>
public sealed record BindingReference
{
    /// <summary>The dataset version that owns the binding.</summary>
    public required long VersionId { get; init; }

    /// <summary>The binding.</summary>
    public required long DimensionBindingId { get; init; }
}
