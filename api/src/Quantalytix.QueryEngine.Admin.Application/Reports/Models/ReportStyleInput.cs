namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>A style of a report, as <c>PUT reports/{reportId}/styles/{styleKey}</c> takes it. A property left null keeps the client's own.</summary>
public sealed record ReportStyleInput
{
    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>row_role</c> value whose rows show the style when they name none; null for none.</summary>
    public string? DefaultRole { get; init; }

    /// <summary>A CSS font family.</summary>
    public string? FontFamily { get; init; }

    /// <summary>The font size in points, from 6 to 72, with at most two decimals.</summary>
    public decimal? FontSize { get; init; }

    /// <summary>A <c>font_weight</c> value.</summary>
    public string? FontWeight { get; init; }

    /// <summary>Whether the text is italic.</summary>
    public required bool IsItalic { get; init; }

    /// <summary>A <c>#RRGGBB</c> text color.</summary>
    public string? TextColor { get; init; }

    /// <summary>A <c>#RRGGBB</c> background color.</summary>
    public string? BackgroundColor { get; init; }

    /// <summary>A <c>border_style</c> value for the rule above the row.</summary>
    public string? BorderTop { get; init; }

    /// <summary>A <c>border_style</c> value for the rule below the row.</summary>
    public string? BorderBottom { get; init; }

    /// <summary>Indent levels, from 0 to 10, added to the row's depth.</summary>
    public int? Indent { get; init; }
}
