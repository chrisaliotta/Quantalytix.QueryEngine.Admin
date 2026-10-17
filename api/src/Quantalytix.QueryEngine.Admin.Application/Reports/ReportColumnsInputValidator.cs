using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a complete column list: unique, well-formed keys other than the reserved <c>line</c> and <c>period</c>, metric keys
/// that name a variable or calculated measure of the report's model version as the value kind says, column IDs of this
/// version, and no removed column that a formula line still places its value in.
/// </summary>
public sealed class ReportColumnsInputValidator : AbstractValidator<ReportColumnsInput>
{
    /// <summary>The keys a report table's schema uses before its columns.</summary>
    private static readonly string[] Reserved = ["line", "period"];

    /// <summary>Declares the rules.</summary>
    /// <param name="metrics">Reads the report's model version metrics.</param>
    /// <param name="columns">Reads the version's columns.</param>
    /// <param name="lines">Reads the version's formula lines.</param>
    /// <param name="route">Names the edited version.</param>
    public ReportColumnsInputValidator(
        ModelMetricsReader metrics,
        ITableGateway<ReportColumnRecord> columns,
        ITableGateway<ReportLineRecord> lines,
        IRequestRoute route)
    {
        RuleForEach(input => input.Columns).ChildRules(column =>
        {
            column.RuleFor(input => input.ColumnKey)
                .MustBeKey(128)
                .Must(key => !Reserved.Contains(key))
                .WithMessage(input => $"'{input.ColumnKey}' names a report table's own column; choose another key.");
            column.RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
            column.RuleFor(input => input.ValueKind).MustBeIn(ReportValueKinds.Definition);
            column.RuleFor(input => input.DisplayFormat).MustFitVarchar(32);
            column.RuleFor(input => input.PayloadJson).MustBeJson();
        });
        RuleFor(input => input.Columns)
            .Must(list => list.DistinctBy(column => column.ColumnKey).Count() == list.Count)
            .WithMessage("'{PropertyName}' must give each column a different key.");
        RuleFor(input => input.Columns).CustomAsync(async (list, context, cancellationToken) =>
        {
            var versionId = route.Id("versionId");
            var model = await metrics.ReadForReportAsync(versionId, cancellationToken);
            foreach (var column in list.Where(column => model.IdOf(column.ValueKind, column.MetricKey) is null))
                context.AddFailure($"Column '{column.ColumnKey}' returns '{column.MetricKey}', which is no {column.ValueKind} metric of the report's model version.");

            var stored = (await columns.ListAsync(new { ReportVersionId = versionId }, cancellationToken)).ToDictionary(column => column.ReportColumnId);
            foreach (var column in list.Where(column => column.ReportColumnId is { } id && !stored.ContainsKey(id)))
                context.AddFailure($"Column {column.ReportColumnId} is not a column of this report version.");
            var removed = stored.Keys.Except(list.Where(column => column.ReportColumnId is not null).Select(column => column.ReportColumnId!.Value)).ToHashSet();
            foreach (var line in (await lines.ListAsync(new { ReportVersionId = versionId }, cancellationToken)).Where(line => line.ValueColumnId is { } id && removed.Contains(id)))
                context.AddFailure($"Formula line '{line.LineKey}' places its value in column '{stored[line.ValueColumnId!.Value].ColumnKey}', so the column must stay.");
        });
    }
}
