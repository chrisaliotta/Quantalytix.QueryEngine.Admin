namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Physical columns of a version's source object to turn into <c>column</c> fields.</summary>
public sealed record FieldsFromColumnsInput
{
    /// <summary>The column names; columns without a logical type or already read by a <c>column</c> field are skipped.</summary>
    public required IReadOnlyList<string> Columns { get; init; }
}
