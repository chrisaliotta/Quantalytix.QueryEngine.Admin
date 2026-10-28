using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The contract's <c>ReportDiagnostics</c>: readiness, coverage and profile resolution, computed by the server (OI-05).</summary>
public sealed record ReportDiagnostics
{
    /// <summary>Whether every report rule passes, so the report can be enabled.</summary>
    public required bool Ready { get; init; }

    /// <summary>The report rules' findings grouped into the editor's readiness steps.</summary>
    public required IReadOnlyList<ReportDiagnosticsStep> Steps { get; init; }

    /// <summary>Each source's coverage, by source key.</summary>
    public required IReadOnlyDictionary<string, ReportSourceCoverage> Coverage { get; init; }

    /// <summary>Each profile's resolution of each variable, by profile key, then variable key.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReportVariableResolution>> Resolution { get; init; }
}

/// <summary>The contract's <c>ReportDiagnosticsStep</c>: one readiness step and the findings that decide it.</summary>
public sealed record ReportDiagnosticsStep
{
    /// <summary>The step: <c>rows</c>, <c>sources</c>, <c>variables</c>, <c>items</c>, <c>profiles</c>, <c>dimensions</c> or <c>columns</c>.</summary>
    public required string Step { get; init; }

    /// <summary><c>ok</c>, <c>warn</c> or <c>fail</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The findings of the step.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}

/// <summary>The contract's <c>ReportSourceCoverage</c>: how a source's items reach the rows.</summary>
public sealed record ReportSourceCoverage
{
    /// <summary>The items that reach no row and are not left out.</summary>
    public required IReadOnlyList<string> Unmapped { get; init; }

    /// <summary>The items that reach more than one row.</summary>
    public required IReadOnlyList<ReportCoverageMulti> Multi { get; init; }

    /// <summary>The items left out on purpose.</summary>
    public required IReadOnlyList<string> LeftOut { get; init; }
}

/// <summary>The contract's <c>ReportCoverageMulti</c>: an item that reaches several rows.</summary>
public sealed record ReportCoverageMulti
{
    /// <summary>The item.</summary>
    public required string ItemKey { get; init; }

    /// <summary>The sum of the factors with which it reaches rows.</summary>
    public required decimal Net { get; init; }

    /// <summary>The rows it reaches.</summary>
    public required IReadOnlyList<string> Rows { get; init; }
}

/// <summary>The contract's <c>ReportVariableResolution</c>: which source a profile reads a variable from, and where it falls short.</summary>
public sealed record ReportVariableResolution
{
    /// <summary><c>ok</c>, <c>shadowed</c>, <c>gap</c>, <c>unresolved</c> or <c>na</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The picked source; null when the profile picks none.</summary>
    public required string? SourceKey { get; init; }

    /// <summary>The data rows in the variable's scope that the picked source does not reach.</summary>
    public required IReadOnlyList<string> RowGaps { get; init; }
}
