using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a complete column list: unique, well-formed keys other than the reserved <c>line</c> and <c>period</c>, metric keys
/// that name a variable or calculated measure of the published version of the report's model as the value kind says, column IDs
/// of this report, optional label and format overrides, and no removed column that a formula row still places its value in.
/// </summary>
public sealed class ReportColumnsInputValidator : AbstractValidator<ReportColumnsInput>
{
    /// <summary>The keys a report table's schema uses before its columns.</summary>
    private static readonly string[] Reserved = ["line", "period"];

    /// <summary>Declares the rules.</summary>
    /// <param name="metrics">Reads the metrics of the published version of the report's model.</param>
    /// <param name="columns">Reads the report's columns.</param>
    /// <param name="lines">Reads the report's formula rows.</param>
    /// <param name="keys">Names the edited report.</param>
    public ReportColumnsInputValidator(
        ModelMetricsReader metrics,
        ITableGateway<ReportColumnRecord> columns,
        ITableGateway<ReportLineRecord> lines,
        ContentKeys keys)
    {
        RuleForEach(input => input.Columns).ChildRules(column =>
        {
            column.RuleFor(input => input.ColumnKey)
                .MustBeKey(128)
                .Must(key => !Reserved.Contains(key))
                .WithMessage(input => $"'{input.ColumnKey}' names a report table's own column; choose another key.");
            column.RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256).When(input => input.DisplayName is not null);
            column.RuleFor(input => input.ValueKind).MustBeIn(ReportValueKinds.Definition);
            column.RuleFor(input => input.DisplayFormat).NotEmpty().MustFitVarchar(32).When(input => input.DisplayFormat is not null);
            column.RuleFor(input => input.PayloadJson).MustBeJson();
        });
        RuleFor(input => input.Columns)
            .Must(list => list.DistinctBy(column => column.ColumnKey).Count() == list.Count)
            .WithMessage("'{PropertyName}' must give each column a different key.");
        RuleFor(input => input.Columns).CustomAsync(async (list, context, cancellationToken) =>
        {
            var owner = new { keys.ReportId };
            var model = await metrics.ReadForReportAsync(keys.ReportId, cancellationToken);
            foreach (var column in list.Where(column => !model.Has(column.ValueKind, column.MetricKey)))
                context.AddFailure($"Column '{column.ColumnKey}' returns '{column.MetricKey}', which is no {column.ValueKind} metric of the published version of the report's model.");

            var stored = (await columns.ListAsync(owner, cancellationToken)).ToDictionary(column => column.ReportColumnId);
            foreach (var column in list.Where(column => column.ReportColumnId is { } id && !stored.ContainsKey(id)))
                context.AddFailure($"Column {column.ReportColumnId} is not a column of this report.");
            var removed = stored.Keys.Except(list.Where(column => column.ReportColumnId is not null).Select(column => column.ReportColumnId!.Value)).ToHashSet();
            foreach (var line in (await lines.ListAsync(owner, cancellationToken)).Where(line => line.ValueColumnId is { } id && removed.Contains(id)))
                context.AddFailure($"Formula row '{line.LineKey}' places its value in column '{stored[line.ValueColumnId!.Value].ColumnKey}', so the column must stay.");
        });
    }
}
