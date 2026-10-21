using System.Text.RegularExpressions;
using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Checks a report's complete row tree before anything is written: each row's fields, the tree's shape
/// (<see cref="ReportRowTree"/>), and against the stored report that a new key is well formed, a stored member row stays a member
/// row and a stored overlay row an overlay row, a row that takes items from bridge sources stays a data row, and each formula row
/// names a calculated measure and a column that resolve.
/// </summary>
public sealed class ReportRowsInputValidator : AbstractValidator<ReportRowsInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="reports">Reads the report.</param>
    /// <param name="owned">Reads the report dimension's version and the bridge sources' bridge versions.</param>
    /// <param name="members">Reads the stored member rows.</param>
    /// <param name="lines">Reads the stored overlay rows.</param>
    /// <param name="sources">Reads the report's sources.</param>
    /// <param name="bridgeMappings">Reads the bridge sources' entries.</param>
    /// <param name="formulaReferences">Checks what formula rows name.</param>
    /// <param name="keys">Names the route's report.</param>
    public ReportRowsInputValidator(
        ITableGateway<ReportRecord> reports,
        OwnedVersions owned,
        ITableGateway<DimensionMemberRecord> members,
        ITableGateway<ReportLineRecord> lines,
        ITableGateway<ReportSourceRecord> sources,
        ITableGateway<BridgeMappingRecord> bridgeMappings,
        FormulaRowReferences formulaReferences,
        ContentKeys keys)
    {
        RuleForEach(input => input.Rows).ChildRules(row =>
        {
            row.RuleFor(input => input.Key).NotEmpty().MustFitVarchar(128);
            row.RuleFor(input => input.Label).NotEmpty().MustFitVarchar(256);
            row.RuleFor(input => input.Kind).MustBeIn(ReportRowKinds.Definition);
            row.RuleFor(input => input.ShowsValues).Equal(true).When(input => input.Kind != ReportRowKinds.Group)
                .WithMessage("Only a group row can hide its total and head the rows under it, so '{PropertyName}' must be true.");
            row.RuleFor(input => input.CalculatedMeasureKey).NotEmpty().When(input => input.Kind == ReportRowKinds.Formula);
            row.RuleFor(input => input.ValueColumnKey).NotEmpty().When(input => input.Kind == ReportRowKinds.Formula);
            row.RuleFor(input => input.CalculatedMeasureKey).Null().When(input => input.Kind != ReportRowKinds.Formula)
                .WithMessage("Only formula rows evaluate a calculated measure, so '{PropertyName}' must be empty.");
            row.RuleFor(input => input.ValueColumnKey).Null().When(input => input.Kind != ReportRowKinds.Formula)
                .WithMessage("Only formula rows place a value in a column, so '{PropertyName}' must be empty.");
        });
        RuleFor(input => input.Rows).Custom((rows, context) =>
        {
            foreach (var problem in ReportRowTree.Problems(rows)) context.AddFailure(problem);
        });
        RuleFor(input => input.Rows).CustomAsync(async (rows, context, cancellationToken) =>
        {
            var owner = new { keys.ReportId };
            var report = await reports.GetAsync(owner, cancellationToken);
            var structure = await owned.StructureAsync(report, cancellationToken);
            var storedMembers = (await members.ListAsync(new { structure.DimensionVersionId }, cancellationToken))
                .Select(member => member.MemberKey)
                .ToHashSet(StringComparer.Ordinal);
            var storedLines = (await lines.ListAsync(owner, cancellationToken)).Select(line => line.LineKey).ToHashSet(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (storedMembers.Contains(row.Key) && !ReportRowKinds.IsMember(row.Kind))
                    context.AddFailure($"Row '{row.Key}' is a member of the report dimension, so it stays a data or group row.");
                else if (storedLines.Contains(row.Key) && ReportRowKinds.IsMember(row.Kind))
                    context.AddFailure($"Row '{row.Key}' is an overlay row, so it stays a section or formula row.");
                else if (!storedMembers.Contains(row.Key) && !storedLines.Contains(row.Key) && !Regex.IsMatch(row.Key, KeyRuleExtensions.KeyPattern))
                    context.AddFailure($"New row key '{row.Key}' must start with a letter and contain only letters, digits and '_'.");
            }

            var bridgeVersions = await owned.BridgesAsync(await sources.ListAsync(owner, cancellationToken), cancellationToken);
            var mapped = (await bridgeMappings.ListAsync(new { BridgeVersionId = bridgeVersions.Values.Select(version => version.BridgeVersionId).ToArray() }, cancellationToken))
                .Select(mapping => mapping.TargetMemberKey)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var row in rows.Where(row => mapped.Contains(row.Key) && row.Kind != ReportRowKinds.Data))
                context.AddFailure($"Row '{row.Key}' takes items from the report's bridge sources, so it stays a data row; move its items first.");

            var formulas = rows.Where(row => row.Kind == ReportRowKinds.Formula).Select(row => (row.Key, row.CalculatedMeasureKey, row.ValueColumnKey));
            foreach (var problem in await formulaReferences.ProblemsAsync(keys.ReportId, formulas, cancellationToken)) context.AddFailure(problem);
        });
    }
}
