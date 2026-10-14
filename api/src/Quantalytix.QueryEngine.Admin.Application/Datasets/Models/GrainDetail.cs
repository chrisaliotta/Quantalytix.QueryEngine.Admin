namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>A dataset grain with its component fields, joined from <c>qe_dataset_grain</c> and <c>qe_dataset_grain_field</c>.</summary>
public sealed record GrainDetail
{
    /// <summary>The grain's surrogate key.</summary>
    public required long GrainId { get; init; }

    /// <summary>Stable metadata name of the grain.</summary>
    public required string GrainKey { get; init; }

    /// <summary>A <c>grain_kind</c> value.</summary>
    public required string GrainKind { get; init; }

    /// <summary>The component fields, in ordinal order.</summary>
    public required IReadOnlyList<long> FieldIds { get; init; }
}
