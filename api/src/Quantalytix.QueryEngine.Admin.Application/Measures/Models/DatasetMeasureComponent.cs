namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>The contract's <c>DatasetMeasureComponent</c>: the field that supplies one role of a measure.</summary>
public sealed record DatasetMeasureComponent
{
    /// <summary>A <c>component_role</c> value.</summary>
    public required string Role { get; init; }

    /// <summary>The field, stored or temporary.</summary>
    public required string FieldId { get; init; }
}
