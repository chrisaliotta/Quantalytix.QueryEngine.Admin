using System.Text.RegularExpressions;
using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Checks a report design's complete row tree before anything is written: each row's fields, the tree's shape
/// (<see cref="DesignRowTree"/>), and against the stored design that a new key is well formed, a stored member row stays a member
/// row and a stored overlay row an overlay row, a row that takes source items stays a data row, a removed row is named by no entry
/// of a mapping set into the report dimension, and each formula row names a calculated measure and a column that resolve.
/// </summary>
public sealed class DesignRowsInputValidator : AbstractValidator<DesignRowsInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="designs">Reads the route's design scope.</param>
    /// <param name="members">Reads the stored member rows.</param>
    /// <param name="lines">Reads the stored overlay rows.</param>
    /// <param name="sources">Reads the variable sources, whose bridges hold the design's row mappings.</param>
    /// <param name="bridgeMappings">Reads the row mappings.</param>
    /// <param name="mappingSets">Reads the mapping sets into the report dimension.</param>
    /// <param name="mappingVersions">Reads their published versions.</param>
    /// <param name="mappingEntries">Reads the entries of those versions.</param>
    /// <param name="metrics">Reads the metrics of the published version of the report's model.</param>
    /// <param name="columns">Reads the version's columns.</param>
    /// <param name="keys">Names the route's report version.</param>
    public DesignRowsInputValidator(
        ReportDesignVersions designs,
        ITableGateway<DimensionMemberRecord> members,
        ITableGateway<ReportLineRecord> lines,
        ITableGateway<VariableSourceRecord> sources,
        ITableGateway<BridgeMappingRecord> bridgeMappings,
        ITableGateway<MappingSetRecord> mappingSets,
        ITableGateway<MappingVersionRecord> mappingVersions,
        ITableGateway<DimensionMappingRecord> mappingEntries,
        ModelMetricsReader metrics,
        ITableGateway<ReportColumnRecord> columns,
        VersionKeys keys)
    {
        RuleForEach(input => input.Rows).ChildRules(row =>
        {
            row.RuleFor(input => input.Key).NotEmpty().MustFitVarchar(128);
            row.RuleFor(input => input.Label).NotEmpty().MustFitVarchar(256);
            row.RuleFor(input => input.Kind).MustBeIn(DesignRowKinds.Definition);
            row.RuleFor(input => input.ShowsValues).Equal(true).When(input => input.Kind != DesignRowKinds.Group)
                .WithMessage("Only a group row can hide its total and head the rows under it, so '{PropertyName}' must be true.");
            row.RuleFor(input => input.CalculatedMeasureKey).NotEmpty().When(input => input.Kind == DesignRowKinds.Formula);
            row.RuleFor(input => input.ValueColumnKey).NotEmpty().When(input => input.Kind == DesignRowKinds.Formula);
            row.RuleFor(input => input.CalculatedMeasureKey).Null().When(input => input.Kind != DesignRowKinds.Formula)
                .WithMessage("Only formula rows evaluate a calculated measure, so '{PropertyName}' must be empty.");
            row.RuleFor(input => input.ValueColumnKey).Null().When(input => input.Kind != DesignRowKinds.Formula)
                .WithMessage("Only formula rows place a value in a column, so '{PropertyName}' must be empty.");
        });
        RuleFor(input => input.Rows).Custom((rows, context) =>
        {
            foreach (var problem in DesignRowTree.Problems(rows)) context.AddFailure(problem);
        });

        var formulaReferences = new FormulaRowReferences(metrics, columns);
        RuleFor(input => input.Rows).CustomAsync(async (rows, context, cancellationToken) =>
        {
            var scope = await designs.ReadAsync(keys.VersionId, cancellationToken);
            var storedMembers = (await members.ListAsync(new { scope.Structure.DimensionVersionId }, cancellationToken))
                .Select(member => member.MemberKey)
                .ToHashSet(StringComparer.Ordinal);
            var storedLines = (await lines.ListAsync(new { ReportVersionId = keys.VersionId }, cancellationToken))
                .Select(line => line.LineKey)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (storedMembers.Contains(row.Key) && !DesignRowKinds.IsMember(row.Kind))
                    context.AddFailure($"Row '{row.Key}' is a member of the report dimension, so it stays a data or group row.");
                else if (storedLines.Contains(row.Key) && DesignRowKinds.IsMember(row.Kind))
                    context.AddFailure($"Row '{row.Key}' is an overlay row, so it stays a section or formula row.");
                else if (!storedMembers.Contains(row.Key) && !storedLines.Contains(row.Key) && !Regex.IsMatch(row.Key, KeyRuleExtensions.KeyPattern))
                    context.AddFailure($"New row key '{row.Key}' must start with a letter and contain only letters, digits and '_'.");
            }

            var mapped = await MappedRowKeysAsync(scope, sources, bridgeMappings, cancellationToken);
            foreach (var row in rows.Where(row => mapped.Contains(row.Key) && row.Kind != DesignRowKinds.Data))
                context.AddFailure($"Row '{row.Key}' takes items from the design's sources, so it stays a data row; move its items first.");

            var keptKeys = rows.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
            var setKeys = (await mappingSets.ListAsync(new { DimensionId = scope.Report.ReportDimensionId }, cancellationToken))
                .ToDictionary(set => set.MappingSetId, set => set.MappingSetKey);
            var setOf = (await mappingVersions.ListAsync(new { MappingSetId = setKeys.Keys.ToArray(), Status = PublicationStatuses.Published }, cancellationToken))
                .ToDictionary(version => version.MappingVersionId, version => setKeys[version.MappingSetId]);
            var entries = await mappingEntries.ListAsync(new { MappingVersionId = setOf.Keys.ToArray() }, cancellationToken);
            foreach (var entry in entries.Where(entry => storedMembers.Contains(entry.MemberKey) && !keptKeys.Contains(entry.MemberKey)))
                context.AddFailure($"Row '{entry.MemberKey}' is named by source code '{entry.SourceKeyValue}' of mapping set '{setOf[entry.MappingVersionId]}'; remove the entry before the row.");

            var formulas = rows.Where(row => row.Kind == DesignRowKinds.Formula).Select(row => (row.Key, row.CalculatedMeasureKey, row.ValueColumnKey));
            foreach (var problem in await formulaReferences.ProblemsAsync(keys.VersionId, formulas, cancellationToken)) context.AddFailure(problem);
        });
    }

    /// <summary>The keys of the rows the design's sources map items to, through the paired versions of their bridges.</summary>
    private static async Task<HashSet<string>> MappedRowKeysAsync(
        ReportDesignScope scope,
        ITableGateway<VariableSourceRecord> sources,
        ITableGateway<BridgeMappingRecord> bridgeMappings,
        CancellationToken cancellationToken)
    {
        var used = (await sources.ListAsync(new { scope.Version.ReportVersionId, ClassificationKind = ClassificationKinds.Bridge }, cancellationToken))
            .Select(source => source.BridgeId!.Value)
            .ToHashSet();
        long[] versionIds = [.. scope.BridgeVersions.Values.Where(version => used.Contains(version.BridgeId)).Select(version => version.BridgeVersionId)];
        return (await bridgeMappings.ListAsync(new { BridgeVersionId = versionIds }, cancellationToken))
            .Select(mapping => mapping.TargetMemberKey)
            .ToHashSet(StringComparer.Ordinal);
    }
}
