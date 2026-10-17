using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a complete line tree: unique, well-formed keys; every parent earlier in the list, so the tree is acyclic and in
/// pre-order; each kind with exactly its own references (a member line its member key, a formula line a calculated measure of
/// the report's model version and one of the version's columns, a section neither); and line IDs of this version. Whether each
/// member key is a member of the report dimension depends on the release, which checks it.
/// </summary>
public sealed class ReportLinesInputValidator : AbstractValidator<ReportLinesInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="metrics">Reads the report's model version metrics.</param>
    /// <param name="columns">Reads the version's columns.</param>
    /// <param name="lines">Reads the version's lines.</param>
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
            line.RuleFor(input => input.PayloadJson).MustBeJson();
            line.RuleFor(input => input.SettingsJson).MustBeJson();
            line.RuleFor(input => input.ReportMemberKey).NotEmpty().MustFitVarchar(128).When(input => input.LineKind == ReportLineKinds.Member);
            line.RuleFor(input => input.ReportMemberKey).Null().When(input => input.LineKind != ReportLineKinds.Member)
                .WithMessage("Only member lines name a report-item member, so '{PropertyName}' must be empty.");
            line.RuleFor(input => input.CalculatedMeasureKey).NotEmpty().When(input => input.LineKind == ReportLineKinds.Formula);
            line.RuleFor(input => input.ValueColumnKey).NotEmpty().When(input => input.LineKind == ReportLineKinds.Formula);
            line.RuleFor(input => input.CalculatedMeasureKey).Null().When(input => input.LineKind != ReportLineKinds.Formula)
                .WithMessage("Only formula lines evaluate a calculated measure, so '{PropertyName}' must be empty.");
            line.RuleFor(input => input.ValueColumnKey).Null().When(input => input.LineKind != ReportLineKinds.Formula)
                .WithMessage("Only formula lines place a value in a column, so '{PropertyName}' must be empty.");
        });
        RuleFor(input => input.Lines).Custom((list, context) =>
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in list)
            {
                if (line.ParentLineKey is { } parent && !seen.Contains(parent))
                    context.AddFailure($"Line '{line.LineKey}' has parent '{parent}', which must come earlier in the list.");
                if (!seen.Add(line.LineKey)) context.AddFailure($"Line key '{line.LineKey}' appears more than once.");
            }
        });
        RuleFor(input => input.Lines).CustomAsync(async (list, context, cancellationToken) =>
        {
            var versionId = route.Id("versionId");
            var model = await metrics.ReadForReportAsync(versionId, cancellationToken);
            var columnKeys = (await columns.ListAsync(new { ReportVersionId = versionId }, cancellationToken)).Select(column => column.ColumnKey).ToHashSet(StringComparer.Ordinal);
            foreach (var line in list.Where(line => line.LineKind == ReportLineKinds.Formula))
            {
                if (line.CalculatedMeasureKey is { } measure && model.IdOf(ReportValueKinds.Calculated, measure) is null)
                    context.AddFailure($"Formula line '{line.LineKey}' evaluates '{measure}', which is no calculated measure of the report's model version.");
                if (line.ValueColumnKey is { } column && !columnKeys.Contains(column))
                    context.AddFailure($"Formula line '{line.LineKey}' places its value in '{column}', which is no column of this report version.");
            }

            var stored = (await lines.ListAsync(new { ReportVersionId = versionId }, cancellationToken)).Select(line => line.ReportLineId).ToHashSet();
            foreach (var line in list.Where(line => line.ReportLineId is { } id && !stored.Contains(id)))
                context.AddFailure($"Line {line.ReportLineId} is not a line of this report version.");
        });
    }
}
