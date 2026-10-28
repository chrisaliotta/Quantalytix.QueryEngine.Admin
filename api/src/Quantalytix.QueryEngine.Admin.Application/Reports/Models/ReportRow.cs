using System.Text.Json;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The contract's <c>ReportRow</c>: a member of the report dimension (<c>data</c>, <c>group</c>) or an overlay line
/// (<c>section</c>, <c>formula</c>) (OI-48). Row keys are fixed once created.
/// </summary>
public sealed record ReportRow
{
    /// <summary>The row key, which bridge rules, scopes and direct sources' field values name it by.</summary>
    public required string Key { get; init; }

    /// <summary>The label the report shows.</summary>
    public required string Label { get; init; }

    /// <summary>A <c>report_row_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>The key of the row it sits under; null for the top level.</summary>
    public required string? ParentKey { get; init; }

    /// <summary>Whether the row shows; a hidden row's children take its place.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Whether a group row shows its total; false makes it a header. True for the other kinds.</summary>
    public required bool ShowsValues { get; init; }

    /// <summary>The calculated measure a formula row evaluates; null for the other kinds.</summary>
    public required string? CalculatedMeasureKey { get; init; }

    /// <summary>The key of the column a formula row places its value in; null for the other kinds.</summary>
    public required string? ValueColumnKey { get; init; }

    /// <summary>The key of the report's style the row shows; null for its role's default.</summary>
    public required string? StyleKey { get; init; }

    /// <summary>The row's own model parameter values, a number or a text as the parameter's type says; missing keys inherit.</summary>
    public required IReadOnlyDictionary<string, JsonElement> Parameters { get; init; }
}

/// <summary>The contract's <c>ReportRowPosition</c>: where a moved row goes.</summary>
public sealed record ReportRowPosition
{
    /// <summary>The new parent's key; null for the top level.</summary>
    public required string? ParentKey { get; init; }

    /// <summary>The index among the new siblings after the move.</summary>
    public required int Index { get; init; }
}
