namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The editable attributes of a dataset grain. <see cref="GrainInput"/> adds the key, which is fixed at creation.</summary>
public record GrainUpdateInput
{
    /// <summary>A <c>grain_kind</c> value: <c>row</c> or <c>series</c>.</summary>
    public required string GrainKind { get; init; }

    /// <summary>The grain's fields in component order; stored with ordinals 1 to n.</summary>
    public required IReadOnlyList<long> FieldIds { get; init; }
}
