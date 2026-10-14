namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Identifies a field of a dataset version from the route; the field must belong to the version.</summary>
public sealed record FieldReference
{
    /// <summary>The dataset version that owns the field.</summary>
    public required long VersionId { get; init; }

    /// <summary>The field.</summary>
    public required long FieldId { get; init; }
}
