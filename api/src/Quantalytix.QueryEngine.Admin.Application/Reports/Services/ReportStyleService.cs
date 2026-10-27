using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The styles of reports, each written whole by key: how the rows that name a style show, and every row of its default role that
/// names none. Styles are report content, so <see cref="ReportChanges"/> checks every write to an enabled report.
/// </summary>
public sealed class ReportStyleService(
    ITableGateway<ReportStyleRecord> styles,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportLineRecord> lines)
{
    /// <summary>Lists a report's styles.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The styles by key.</returns>
    public async Task<IReadOnlyList<ReportStyleRecord>> ListAsync(long reportId, CancellationToken cancellationToken)
    {
        return [.. (await styles.ListAsync(new { ReportId = reportId }, cancellationToken)).OrderBy(style => style.StyleKey, StringComparer.Ordinal)];
    }

    /// <summary>The style an input describes under a key.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="styleKey">The validated key.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The style to store.</returns>
    public static ReportStyleRecord Compose(long reportId, string styleKey, ReportStyleInput input)
    {
        return new ReportStyleRecord
        {
            ReportId = reportId,
            StyleKey = styleKey,
            DisplayName = input.DisplayName,
            DefaultRole = input.DefaultRole,
            FontFamily = input.FontFamily,
            FontSize = input.FontSize,
            FontWeight = input.FontWeight,
            IsItalic = input.IsItalic,
            TextColor = input.TextColor,
            BackgroundColor = input.BackgroundColor,
            BorderTop = input.BorderTop,
            BorderBottom = input.BorderBottom,
            Indent = input.Indent
        };
    }

    /// <summary>The report with a style added, or in place of the stored one with its key.</summary>
    /// <param name="report">The report as it stands.</param>
    /// <param name="style">The composed style.</param>
    /// <returns>The report as the write leaves it.</returns>
    public static ReportGraph Put(ReportGraph report, ReportStyleRecord style)
    {
        return report with { Styles = [.. Without(report, style.StyleKey).Styles, style] };
    }

    /// <summary>The report without a style.</summary>
    /// <param name="report">The report as it stands.</param>
    /// <param name="styleKey">The style.</param>
    /// <returns>The report as the delete leaves it.</returns>
    public static ReportGraph Without(ReportGraph report, string styleKey)
    {
        return report with { Styles = [.. report.Styles.Where(style => style.StyleKey != styleKey)] };
    }

    /// <summary>Stores a style in place of the one with its key; a primary key insert replaces the stored row.</summary>
    /// <param name="style">A style the report gate let through.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the style is stored.</returns>
    public Task ReplaceAsync(ReportStyleRecord style, CancellationToken cancellationToken)
    {
        return styles.InsertAsync(style, cancellationToken);
    }

    /// <summary>Deletes a style, unless a row of the report shows it.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="styleKey">The style.</param>
    /// <param name="cancellationToken">Cancels the reads and the delete.</param>
    /// <returns>Null when the style was deleted, or the report had none by the key; otherwise the detail naming the rows that show it.</returns>
    public async Task<string?> DeleteAsync(long reportId, string styleKey, CancellationToken cancellationToken)
    {
        var style = new { ReportId = reportId, StyleKey = styleKey };
        var rows = (await memberOptions.ListAsync(style, cancellationToken)).Select(option => option.MemberKey)
            .Concat((await lines.ListAsync(style, cancellationToken)).Select(line => line.LineKey));
        var blockers = new DeleteBlockers($"Style '{styleKey}'");
        blockers.AddNamed("referenced_by_report_row", "shown by rows", rows.Select(row => $"'{row}'"));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;
        await styles.DeleteAsync(style, cancellationToken);
        return null;
    }
}
