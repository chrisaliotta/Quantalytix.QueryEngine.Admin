namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>The dataset measure that supplies one model variable from a report source.</summary>
public sealed record SourceVariableInput
{
    /// <summary>The key of a variable of the report's model.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The enabled dataset measure that supplies the variable.</summary>
    public required string MeasureKey { get; init; }

    /// <summary>Optional structured filter over the dataset's filterable fields, in the query contract's <c>filterNode</c> vocabulary.</summary>
    public string? SourceFilterJson { get; init; }

    /// <summary>
    /// Optional <c>variable_missing_behavior</c> value that replaces the variable's own for this source alone, such as <c>null</c> for a
    /// budget source with no rows for some lines; null keeps the variable's.
    /// </summary>
    public string? MissingBehavior { get; init; }
}
