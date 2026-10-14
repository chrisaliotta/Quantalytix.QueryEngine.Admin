namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_measure_component</c> row: associates a source field with a role of a measure.</summary>
public sealed record MeasureComponentRecord
{
    /// <summary>The owning measure; first part of the primary key.</summary>
    public required long MeasureId { get; init; }

    /// <summary>A <c>component_role</c> value; second part of the primary key.</summary>
    public required string ComponentRole { get; init; }

    /// <summary>The field that supplies the role.</summary>
    public required long FieldId { get; init; }
}
