namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The contract's <c>ReportProfile</c>: a source profile with the source it picks for each variable.</summary>
public sealed record ReportProfile
{
    /// <summary>The profile key.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether requests may run under the profile.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>The picked source key, by variable key.</summary>
    public required IReadOnlyDictionary<string, string> Picks { get; init; }
}

/// <summary>The contract's <c>ReportCalculationDimension</c>: a role runs break out and filter by.</summary>
public sealed record ReportCalculationDimension
{
    /// <summary>The key requests and defaults name it by.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The role every source reads.</summary>
    public required long RoleId { get; init; }

    /// <summary>How the role's members group by default.</summary>
    public required Grouping Grouping { get; init; }

    /// <summary>Whether runs may break out by it.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Whether filters may compare it.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Whether the engine reads it.</summary>
    public required bool IsEnabled { get; init; }
}

/// <summary>The contract's <c>ReportScenario</c>: a role whose members a run reads one at a time; it takes the role's name (OI-50).</summary>
public sealed record ReportScenario
{
    /// <summary>The role.</summary>
    public required long RoleId { get; init; }
}

/// <summary>The contract's <c>ReportVariableScope</c>: the member of the report dimension one variable reads (OI-50).</summary>
public sealed record ReportVariableScope
{
    /// <summary>The model variable.</summary>
    public required string VariableKey { get; init; }

    /// <summary>The member row the variable is scoped to.</summary>
    public required string ScopeMemberKey { get; init; }

    /// <summary>A <c>selection_scope</c> value: <c>exact</c> or <c>descendants</c>.</summary>
    public required string SelectionScope { get; init; }
}

/// <summary>The contract's <c>ReportColumn</c>: one returned metric; the list order is the position.</summary>
public sealed record ReportColumn
{
    /// <summary>The column key.</summary>
    public required string Key { get; init; }

    /// <summary>A label replacing the metric's; null keeps the metric's.</summary>
    public required string? DisplayName { get; init; }

    /// <summary>A <c>report_value_kind</c> value.</summary>
    public required string ValueKind { get; init; }

    /// <summary>The variable or calculated measure key, as the value kind says.</summary>
    public required string MetricKey { get; init; }

    /// <summary>A presentation hint replacing the metric's; null keeps the metric's.</summary>
    public required string? DisplayFormat { get; init; }

    /// <summary>Whether a request without columns returns it.</summary>
    public required bool IsVisible { get; init; }
}

/// <summary>The contract's <c>ReportStyle</c>: how rows that name it, or rows of its default role, show; null keeps the client's own.</summary>
public sealed record ReportStyle
{
    /// <summary>The style key rows name it by.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>row_role</c> value whose rows show it when they name none; null for none.</summary>
    public required string? DefaultRole { get; init; }

    /// <summary>A CSS font family.</summary>
    public required string? FontFamily { get; init; }

    /// <summary>The font size in points.</summary>
    public required decimal? FontSize { get; init; }

    /// <summary>A <c>font_weight</c> value.</summary>
    public required string? FontWeight { get; init; }

    /// <summary>Whether the text is italic.</summary>
    public required bool IsItalic { get; init; }

    /// <summary>A <c>#RRGGBB</c> text color.</summary>
    public required string? TextColor { get; init; }

    /// <summary>A <c>#RRGGBB</c> background color.</summary>
    public required string? BackgroundColor { get; init; }

    /// <summary>A <c>border_style</c> value above the row.</summary>
    public required string? BorderTop { get; init; }

    /// <summary>A <c>border_style</c> value below the row.</summary>
    public required string? BorderBottom { get; init; }

    /// <summary>Indent levels added to the row's depth.</summary>
    public required int? Indent { get; init; }
}
