namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetVersionSettings</c>: the version row's own columns (<c>qe_dataset_version</c>).</summary>
public sealed record DatasetVersionSettings
{
    /// <summary>A <c>source_object_kind</c> value.</summary>
    public required string SourceObjectKind { get; init; }

    /// <summary>The table or view the version reads.</summary>
    public required string SourceObject { get; init; }

    /// <summary>Whether the version may satisfy aggregate queries.</summary>
    public required bool SupportsAggregate { get; init; }

    /// <summary>Whether the version may satisfy detail queries.</summary>
    public required bool SupportsDetail { get; init; }

    /// <summary>The default time binding, stored or temporary.</summary>
    public required string? DefaultTimeBindingId { get; init; }

    /// <summary>The expression grammar version of the version's expressions.</summary>
    public required string ExpressionLanguageVersion { get; init; }

    /// <summary>The authorization policy identifier.</summary>
    public required string? SecurityPolicyKey { get; init; }

    /// <summary>Free JSON text without query semantics.</summary>
    public required string Properties { get; init; }
}
