namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_report_row_parameter</c> row: the value one row of a report sets for a parameter of the report's model, which the rows
/// under it inherit unless they set their own. Exactly one value is set, by the parameter's type.
/// </summary>
public sealed record ReportRowParameterRecord
{
    /// <summary>The owning report; first part of the primary key.</summary>
    public required long ReportId { get; init; }

    /// <summary>A member key of the report dimension or an overlay row's key; second part of the primary key.</summary>
    public required string RowKey { get; init; }

    /// <summary>The model parameter the value is for; third part of the primary key.</summary>
    public required string ParameterKey { get; init; }

    /// <summary>The value of a number parameter; null for a text parameter.</summary>
    public decimal? NumberValue { get; init; }

    /// <summary>The value of a text parameter; null for a number parameter.</summary>
    public string? TextValue { get; init; }
}
