using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a complete overlay row list: unique, well-formed keys; every overlay parent earlier in the list, so overlay rows nest
/// without cycles; each kind with exactly its own references (a formula row a calculated measure of the published version of the
/// report's model and one of the version's columns, a section neither); and row IDs of this version. Whether a parent that is no
/// overlay row is a member of the report's dimension, and whether a key collides with a member key, depends on its published
/// version, which <c>report_items_resolve</c> checks.
/// </summary>
public sealed class ReportLinesInputValidator : AbstractValidator<ReportLinesInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="metrics">Reads the metrics of the published version of the report's model.</param>
    /// <param name="columns">Reads the version's columns.</param>
    /// <param name="lines">Reads the version's overlay rows.</param>
    /// <param name="route">Names the edited version.</param>
    public ReportLinesInputValidator(
        ModelMetricsReader metrics,
        ITableGateway<ReportColumnRecord> columns,
        ITableGateway<ReportLineRecord> lines,
        IRequestRoute route)
    {
        RuleForEach(input => input.Lines).ChildRules(line =>
        {
            line.RuleFor(input => input.LineKey).MustBeKey(128);
            line.RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
            line.RuleFor(input => input.LineKind).MustBeIn(ReportLineKinds.Definition);
            line.RuleFor(input => input.ParentKey).MustFitVarchar(128);
            line.RuleFor(input => input.PayloadJson).MustBeJson();
            line.RuleFor(input => input.SettingsJson).MustBeJson();
            line.RuleFor(input => input.CalculatedMeasureKey).NotEmpty().When(input => input.LineKind == ReportLineKinds.Formula);
            line.RuleFor(input => input.ValueColumnKey).NotEmpty().When(input => input.LineKind == ReportLineKinds.Formula);
            line.RuleFor(input => input.CalculatedMeasureKey).Null().When(input => input.LineKind != ReportLineKinds.Formula)
                .WithMessage("Only formula rows evaluate a calculated measure, so '{PropertyName}' must be empty.");
            line.RuleFor(input => input.ValueColumnKey).Null().When(input => input.LineKind != ReportLineKinds.Formula)
                .WithMessage("Only formula rows place a value in a column, so '{PropertyName}' must be empty.");
        });
        RuleFor(input => input.Lines).Custom((list, context) =>
        {
            var keys = list.Select(line => line.LineKey).ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in list)
            {
                if (line.ParentKey is { } parent && keys.Contains(parent) && !seen.Contains(parent))
                    context.AddFailure($"Row '{line.LineKey}' sits under overlay row '{parent}', which must come earlier in the list.");
                if (!seen.Add(line.LineKey)) context.AddFailure($"Row key '{line.LineKey}' appears more than once.");
            }
        });
        RuleFor(input => input.Lines).CustomAsync(async (list, context, cancellationToken) =>
        {
            var versionId = route.Id("versionId");
            var model = await metrics.ReadForReportAsync(versionId, cancellationToken);
            var columnKeys = (await columns.ListAsync(new { ReportVersionId = versionId }, cancellationToken)).Select(column => column.ColumnKey).ToHashSet(StringComparer.Ordinal);
            foreach (var line in list.Where(line => line.LineKind == ReportLineKinds.Formula))
            {
                if (line.CalculatedMeasureKey is { } measure && !model.Has(ReportValueKinds.Calculated, measure))
                    context.AddFailure($"Formula row '{line.LineKey}' evaluates '{measure}', which is no calculated measure of the published version of the report's model.");
                if (line.ValueColumnKey is { } column && !columnKeys.Contains(column))
                    context.AddFailure($"Formula row '{line.LineKey}' places its value in '{column}', which is no column of this report version.");
            }

            var stored = (await lines.ListAsync(new { ReportVersionId = versionId }, cancellationToken)).Select(line => line.ReportLineId).ToHashSet();
            foreach (var line in list.Where(line => line.ReportLineId is { } id && !stored.Contains(id)))
                context.AddFailure($"Row {line.ReportLineId} is not an overlay row of this report version.");
        });
    }
}
