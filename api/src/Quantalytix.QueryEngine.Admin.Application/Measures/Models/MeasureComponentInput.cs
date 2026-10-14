namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>A field a measure reads, in one role (schema section 13).</summary>
public sealed record MeasureComponentInput
{
    /// <summary>A <c>component_role</c> value, such as <c>value</c> or <c>weight</c>; each role appears once per measure.</summary>
    public required string ComponentRole { get; init; }

    /// <summary>The field of the measure's dataset version that supplies the role.</summary>
    public required long FieldId { get; init; }
}
