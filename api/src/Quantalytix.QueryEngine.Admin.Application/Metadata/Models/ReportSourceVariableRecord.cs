namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_report_source_variable</c> row: the dataset measure a report source supplies one variable of the report's model with,
/// and an optional filter over the dataset's fields.
/// </summary>
public sealed record ReportSourceVariableRecord
{
    /// <summary>The source; first part of the primary key.</summary>
    public required long ReportSourceId { get; init; }

    /// <summary>The key of the model variable; second part of the primary key.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The dataset measure that supplies the variable.</summary>
    public required string MeasureKey { get; init; }

    /// <summary>Optional structured filter over the dataset's fields, applied before aggregation.</summary>
    public string? SourceFilterJson { get; init; }

    /// <summary>A <c>variable_missing_behavior</c> value that replaces the variable's own for this source alone; null keeps the variable's.</summary>
    public string? MissingBehavior { get; init; }
}
