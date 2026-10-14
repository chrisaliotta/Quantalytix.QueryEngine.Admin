namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The editable settings of a draft dataset version.</summary>
public sealed record DatasetVersionSettingsInput
{
    /// <summary>A <c>source_object_kind</c> value.</summary>
    public required string SourceObjectKind { get; init; }

    /// <summary>The table or view the version queries.</summary>
    public required string SourceObject { get; init; }

    /// <summary>Whether the dataset may satisfy aggregate mode.</summary>
    public required bool SupportsAggregate { get; init; }

    /// <summary>Whether the dataset may satisfy detail mode.</summary>
    public required bool SupportsDetail { get; init; }

    /// <summary>The time binding queries use when a measure or query requires no other; a binding of this version.</summary>
    public long? DefaultTimeBindingId { get; init; }

    /// <summary>Version of the expression grammar the stored field and measure expressions use, such as <c>1.0</c>.</summary>
    public required string ExpressionLanguageVersion { get; init; }

    /// <summary>Optional application authorization policy identifier.</summary>
    public string? SecurityPolicyKey { get; init; }

    /// <summary>Extension metadata as raw JSON text, or null.</summary>
    public string? Properties { get; init; }
}
