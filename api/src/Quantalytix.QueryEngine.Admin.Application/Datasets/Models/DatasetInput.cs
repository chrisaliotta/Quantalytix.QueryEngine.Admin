namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>A new dataset: its identity and the source object its first draft version queries.</summary>
public sealed record DatasetInput : DatasetUpdateInput
{
    /// <summary>Stable logical dataset ID that requests name, such as <c>mortgage.loan_monthly</c>; fixed once created.</summary>
    public required string DatasetKey { get; init; }

    /// <summary>The <c>source_object_kind</c> of the first version's source object.</summary>
    public required string SourceObjectKind { get; init; }

    /// <summary>The table or view the first version queries.</summary>
    public required string SourceObject { get; init; }
}
