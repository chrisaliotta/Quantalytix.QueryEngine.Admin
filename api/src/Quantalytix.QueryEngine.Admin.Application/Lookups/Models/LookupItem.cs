namespace Quantalytix.QueryEngine.Admin.Application.Lookups;

/// <summary>One option of a picker: a metadata row reduced to what a select list shows.</summary>
public sealed record LookupItem
{
    /// <summary>The row's surrogate key, which forms reference.</summary>
    public required long Id { get; init; }

    /// <summary>The row's stable key, such as a dataset key or <c>v2</c> for a version.</summary>
    public required string Key { get; init; }

    /// <summary>The text the picker shows.</summary>
    public required string Label { get; init; }

    /// <summary>A related row's ID where the kind defines one, such as a member's parent member.</summary>
    public long? ParentId { get; init; }

    /// <summary>The <c>publication_status</c> or <c>assumption_set_status</c> of versioned rows.</summary>
    public string? Status { get; init; }

    /// <summary>A vocabulary value that classifies the row, such as a field's data type.</summary>
    public string? Classifier { get; init; }
}
