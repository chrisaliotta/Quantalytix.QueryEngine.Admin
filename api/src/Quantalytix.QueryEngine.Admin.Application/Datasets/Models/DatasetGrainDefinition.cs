namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetGrainDefinition</c>: the writable content of a grain and its ordered components.</summary>
public abstract record DatasetGrainDefinition
{
    /// <summary>The grain key, fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>A <c>grain_kind</c> value.</summary>
    public required string GrainKind { get; init; }

    /// <summary>The component fields in ordinal order, stored or temporary.</summary>
    public required IReadOnlyList<string> FieldIds { get; init; }
}
