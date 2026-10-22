namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_report_style</c> row: how a report shows the rows that name it, or every row of its default role that names no style.
/// Styles are presentation only; a property left null keeps the client's own.
/// </summary>
public sealed record ReportStyleRecord
{
    /// <summary>The owning report; first part of the primary key.</summary>
    public required long ReportId { get; init; }

    /// <summary>The key rows name the style by; second part of the primary key.</summary>
    public required string StyleKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>row_role</c> value whose rows show the style when they name none; at most one style of a report per role.</summary>
    public string? DefaultRole { get; init; }

    /// <summary>A CSS font family.</summary>
    public string? FontFamily { get; init; }

    /// <summary>The font size in points, from 6 to 72.</summary>
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
