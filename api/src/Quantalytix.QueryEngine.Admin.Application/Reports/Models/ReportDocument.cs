namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The contract's <c>ReportDocument</c>: the whole report, assembled from the tables that own each fact (OI-48 to OI-50).</summary>
public sealed record ReportDocument
{
    /// <summary>The report.</summary>
    public required long Id { get; init; }

    /// <summary>The report's fixed key.</summary>
    public required string Key { get; init; }

    /// <summary>Whether the engine offers the report.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The calculation model the report reads.</summary>
    public required long ModelId { get; init; }

    /// <summary>The report dimension, whose members are the member rows.</summary>
    public required long ReportDimensionId { get; init; }

    /// <summary>The report dimension's one version.</summary>
    public required long ReportDimensionVersionId { get; init; }

    /// <summary>The settings and defaults.</summary>
    public required ReportSettings Settings { get; init; }

    /// <summary>Every row, in hierarchy order.</summary>
    public required IReadOnlyList<ReportRow> Rows { get; init; }

    /// <summary>The sources, by key.</summary>
    public required IReadOnlyList<ReportSource> Sources { get; init; }

    /// <summary>The source profiles, by key.</summary>
    public required IReadOnlyList<ReportProfile> Profiles { get; init; }

    /// <summary>The calculation dimensions, by key.</summary>
    public required IReadOnlyList<ReportCalculationDimension> CalculationDimensions { get; init; }

    /// <summary>The scenarios.</summary>
    public required IReadOnlyList<ReportScenario> Scenarios { get; init; }

    /// <summary>The variable scopes, by variable key.</summary>
    public required IReadOnlyList<ReportVariableScope> VariableScopes { get; init; }

    /// <summary>The columns in their order.</summary>
    public required IReadOnlyList<ReportColumn> Columns { get; init; }

    /// <summary>The styles, by key.</summary>
    public required IReadOnlyList<ReportStyle> Styles { get; init; }
}
