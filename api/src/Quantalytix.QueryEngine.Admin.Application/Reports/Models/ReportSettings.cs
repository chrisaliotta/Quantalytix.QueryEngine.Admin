
namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The contract's <c>ReportSettings</c>: a report's identity attributes and defaults, typed; the periodicity and layout live in
/// <c>default_settings_json</c>, the breakout in <c>default_pivot_json</c> and the filter in <c>default_filter_json</c> (OI-54).
/// </summary>
public sealed record ReportSettings
{
    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification.</summary>
    public required string ReportType { get; init; }

    /// <summary>A <c>subtotal_position</c> value.</summary>
    public required string SubtotalPosition { get; init; }

    /// <summary>The profile a request runs under when it names none; null for none.</summary>
    public required string? DefaultProfileKey { get; init; }

    /// <summary>A <c>periodicity</c> value; null for none.</summary>
    public required string? Periodicity { get; init; }

    /// <summary>A <c>report_layout</c> value.</summary>
    public required string Layout { get; init; }

    /// <summary>The calculation dimension keys a request breaks out by when it names none.</summary>
    public required IReadOnlyList<string> DefaultBreakout { get; init; }

    /// <summary>The filter over calculation dimension keys every request takes; null for none.</summary>
    public required FilterNode? DefaultFilter { get; init; }
}
