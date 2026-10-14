namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The existing physical column a <c>json_path</c> or <c>expression</c> field reads instead of computing its value.</summary>
public sealed record MaterializedColumnInput
{
    /// <summary>A column of the version's source object, or null to compute the value again.</summary>
    public string? ColumnName { get; init; }
}
