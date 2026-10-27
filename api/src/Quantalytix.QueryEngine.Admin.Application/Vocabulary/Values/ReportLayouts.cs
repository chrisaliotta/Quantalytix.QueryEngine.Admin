namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// The <c>report_layout</c> vocabulary, a report's settings' <c>layout</c>: how a request's result stage, its result filter, ordering
/// and paging, treats the report's rows.
/// </summary>
public static class ReportLayouts
{
    /// <summary>The lines are the report's structure: a request filters, orders and pages only the breakout rows within each line and period.</summary>
    public const string Statement = "statement";

    /// <summary>The lines are a list: a request filters, orders and pages the whole table.</summary>
    public const string List = "list";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "report_layout",
        Key = "layout",
        Description = "How a request's result filter, ordering and paging treat a report's rows.",
        Values =
        [
            new() { Value = Statement, Label = "Statement", Description = "The lines and periods keep their order; requests filter, order and page breakout rows within each." },
            new() { Value = List, Label = "List", Description = "Requests filter, order and page the whole table." }
        ]
    };
}
