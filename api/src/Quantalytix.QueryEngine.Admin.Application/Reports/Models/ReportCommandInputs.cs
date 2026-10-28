using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The contract's <c>ReportCreateInput</c>: a new, disabled report over a model, with a single root row, the published rows of a
/// start-from dimension, or a copy of another report (which is also how a report changes its model, OI-47).
/// </summary>
public sealed record ReportCreateInput
{
    /// <summary>The fixed key, which the report dimension takes too.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification.</summary>
    public required string ReportType { get; init; }

    /// <summary>The calculation model the report reads; fixed.</summary>
    public required long ModelId { get; init; }

    /// <summary>A categorical or hierarchical dimension whose published members become the rows.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? StartFromDimensionId { get; init; }

    /// <summary>A report whose rows, sources and content are copied.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? CopyFromReportId { get; init; }
}

/// <summary>The contract's <c>ReportEnabledInput</c>.</summary>
public sealed record ReportEnabledInput
{
    /// <summary>True enables the report, which every report rule must pass; false disables it.</summary>
    public required bool IsEnabled { get; init; }
}
