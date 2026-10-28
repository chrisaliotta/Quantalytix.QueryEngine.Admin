using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Computes a report's diagnostics from its document and its report rules' violations, stored or pending (OI-05): the rules grouped
/// into the editor's readiness steps, each source's coverage, and each profile's resolution of each variable. A bridge source's items
/// are the assignable members of the published version of its role's dimension; a rule reaches its item, or with <c>descendants</c>
/// every item under it. A direct source's items are the values of its field in the active release's version of its dataset, read
/// through <see cref="DatasetFieldValues"/>; a value reaches the data row of the same key. A direct source whose values cannot be
/// read in full (an expression field, more values than the read's limit, or a dataset or field the active release lacks) reports
/// no row gaps, since no row is known to be missed.
/// </summary>
/// <param name="roles">Reads the sources' roles.</param>
/// <param name="publishedDimensions">Reads the published version of each role's dimension.</param>
/// <param name="variables">Reads the model's variables.</param>
/// <param name="measures">Reads the model's calculated measures.</param>
/// <param name="releaseDatasets">Reads the active release's version of a direct source's dataset.</param>
/// <param name="fieldValues">Reads a direct source's field values.</param>
public sealed class ReportDiagnosticsBuilder(
    ITableGateway<DimensionRoleRecord> roles,
    PublishedDimensionReader publishedDimensions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> measures,
    ReleaseDatasets releaseDatasets,
    DatasetFieldValues fieldValues)
{
    /// <summary>The code of the coverage warnings of the <c>items</c> step, which no report rule raises.</summary>
    public const string UnmappedItemsCode = "report_items_unmapped";

    /// <summary>The readiness step of each report rule.</summary>
    private static readonly Dictionary<string, string> StepOf = new(StringComparer.Ordinal)
    {
        [ReportItemsResolveRule.Code] = "rows",
        [ReportRowParametersResolveRule.Code] = "rows",
        [ReportStylesResolveRule.Code] = "rows",
        [ReportSourcesResolveRule.Code] = "sources",
        [ReportFactorsApplyRule.Code] = "sources",
        [ReportMetricsResolveRule.Code] = "variables",
        [DefaultProfileEnabledRule.Code] = "profiles",
        [ProfilesCompleteRule.Code] = "profiles",
        [CalculationGroupingResolveRule.Code] = "dimensions",
        [ScenarioRolesDistinctRule.Code] = "dimensions",
        [ReportDefaultsResolveRule.Code] = "dimensions",
        [ColumnKeysDistinctRule.Code] = "columns"
    };

    /// <summary>The steps, in the editor's order.</summary>
    private static readonly string[] Steps = ["rows", "sources", "variables", "items", "profiles", "dimensions", "columns"];

    /// <summary>The diagnostics of a report.</summary>
    /// <param name="document">The report, stored or pending.</param>
    /// <param name="violations">Every report rule's violations of it.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The diagnostics.</returns>
    public async Task<ReportDiagnostics> BuildAsync(ReportDocument document, IReadOnlyList<RuleViolation> violations, CancellationToken cancellationToken)
    {
        var dataRows = DataRows(document);
        var coverage = new Dictionary<string, ReportSourceCoverage>(StringComparer.Ordinal);
        var reached = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var source in document.Sources)
        {
            if (source.RoleId is { } roleId)
            {
                coverage[source.Key] = await BridgeCoverageAsync(source, roleId, cancellationToken);
                continue;
            }

            var values = await DirectValuesAsync(source, cancellationToken);
            coverage[source.Key] = DirectCoverage(values, dataRows);
            if (values is { IsComplete: true }) reached[source.Key] = values.Values.ToHashSet(StringComparer.Ordinal);
        }

        var warnings = coverage
            .Where(entry => entry.Value.Unmapped.Count > 0)
            .Select(entry => new RuleViolation
            {
                Code = UnmappedItemsCode,
                Message = $"Source '{entry.Key}' maps {entry.Value.Unmapped.Count} items to no row: {string.Join(", ", entry.Value.Unmapped.Select(item => $"'{item}'"))}.",
                Target = ReportAreas.Target(ReportAreas.Sources, entry.Key)
            })
            .ToList();
        return new ReportDiagnostics
        {
            Ready = violations.Count == 0,
            Steps = [.. Steps.Select(step => Step(step, step == "items" ? warnings : [.. violations.Where(violation => StepOfViolation(violation) == step)]))],
            Coverage = coverage,
            Resolution = await ResolutionAsync(document, reached, cancellationToken)
        };
    }

    /// <summary>The coverage of one source.</summary>
    /// <param name="document">The source's report.</param>
    /// <param name="source">A source of the report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Its unmapped, multiply mapped and left-out items.</returns>
    public async Task<ReportSourceCoverage> CoverageAsync(ReportDocument document, ReportSource source, CancellationToken cancellationToken)
    {
        if (source.RoleId is { } roleId) return await BridgeCoverageAsync(source, roleId, cancellationToken);
        return DirectCoverage(await DirectValuesAsync(source, cancellationToken), DataRows(document));
    }

    /// <summary>A bridge source's coverage: the assignable members of its role's dimension, each by the rows its rules reach.</summary>
    private async Task<ReportSourceCoverage> BridgeCoverageAsync(ReportSource source, long roleId, CancellationToken cancellationToken)
    {
        var role = await roles.GetAsync(new { DimensionRoleId = roleId }, cancellationToken);
        if (await publishedDimensions.ReadAsync(role.DimensionId, cancellationToken) is not { } items)
            return new ReportSourceCoverage { Unmapped = [], Multi = [], LeftOut = source.LeftOut };

        var leftOut = source.LeftOut.ToHashSet(StringComparer.Ordinal);
        var unmapped = new List<string>();
        var multi = new List<ReportCoverageMulti>();
        foreach (var item in items.MemberKeys.Where(key => !items.UnassignableMemberKeys.Contains(key)).Order(StringComparer.Ordinal))
        {
            var ancestors = items.SelfAndAncestors(item).ToHashSet(StringComparer.Ordinal);
            var byRow = source.Rules
                .Select(row => (Row: row.Key, Rules: row.Value.Where(rule => rule.Selection == SourceSelections.Member ? rule.ItemKey == item : ancestors.Contains(rule.ItemKey)).ToList()))
                .Where(row => row.Rules.Count > 0)
                .ToDictionary(row => row.Row, row => row.Rules.Sum(rule => rule.Factor), StringComparer.Ordinal);
            if (byRow.Count == 0)
            {
                if (!leftOut.Contains(item)) unmapped.Add(item);
                continue;
            }

            string[] rows = [.. byRow.Where(row => row.Value != 0m).Select(row => row.Key).Order(StringComparer.Ordinal)];
            if (rows.Length > 1) multi.Add(new ReportCoverageMulti { ItemKey = item, Net = byRow.Values.Sum(), Rows = rows });
        }

        return new ReportSourceCoverage { Unmapped = unmapped, Multi = multi, LeftOut = source.LeftOut };
    }

    /// <summary>A direct source's coverage: the field values that name no data row; a value reaches one row at most.</summary>
    private static ReportSourceCoverage DirectCoverage(FieldValues? values, IReadOnlySet<string> dataRows)
    {
        return new ReportSourceCoverage { Unmapped = [.. values?.Values.Where(value => !dataRows.Contains(value)) ?? []], Multi = [], LeftOut = [] };
    }

    /// <summary>A direct source's field values in the active release; null when the release lacks the dataset or field.</summary>
    private async Task<FieldValues?> DirectValuesAsync(ReportSource source, CancellationToken cancellationToken)
    {
        if (await releaseDatasets.ActiveAsync(source.DatasetId, cancellationToken) is not { } dataset) return null;
        if (!dataset.Fields.TryGetValue(source.FieldKey!, out var field)) return null;
        return await fieldValues.ReadAsync(field, cancellationToken);
    }

    /// <summary>The keys of a report's data rows.</summary>
    private static IReadOnlySet<string> DataRows(ReportDocument document)
    {
        return document.Rows.Where(row => row.Kind == ReportRowKinds.Data).Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Each profile's resolution of each variable the report reads or a source supplies: <c>na</c> for a variable the report does not
    /// read; <c>unresolved</c> when the profile picks no source that supplies it; <c>gap</c> when the picked source reaches only some
    /// data rows in the variable's scope; <c>shadowed</c> when another source supplies it too; <c>ok</c> otherwise.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReportVariableResolution>>> ResolutionAsync(
        ReportDocument document,
        IReadOnlyDictionary<string, IReadOnlySet<string>> reached,
        CancellationToken cancellationToken)
    {
        var model = new { CalculationModelId = document.ModelId };
        var graph = new FormulaGraph(await variables.ListAsync(model, cancellationToken), await measures.ListAsync(model, cancellationToken));
        var read = document.Columns.Select(column => column.MetricKey)
            .Concat(document.Rows.Select(row => row.CalculatedMeasureKey).OfType<string>())
            .SelectMany(graph.Variables)
            .ToHashSet(StringComparer.Ordinal);
        var suppliers = document.Sources
            .SelectMany(source => source.Variables.Select(variable => (variable.VariableKey, Source: source)))
            .ToLookup(entry => entry.VariableKey, entry => entry.Source, StringComparer.Ordinal);
        string[] resolved = [.. read.Concat(suppliers.Select(group => group.Key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return document.Profiles.ToDictionary(
            profile => profile.Key,
            profile => (IReadOnlyDictionary<string, ReportVariableResolution>)resolved.ToDictionary(
                variable => variable,
                variable => Resolve(document, profile, variable, read.Contains(variable), [.. suppliers[variable]], reached),
                StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    /// <summary>One profile's resolution of one variable.</summary>
    private static ReportVariableResolution Resolve(
        ReportDocument document,
        ReportProfile profile,
        string variable,
        bool isRead,
        IReadOnlyList<ReportSource> suppliers,
        IReadOnlyDictionary<string, IReadOnlySet<string>> reached)
    {
        var pickedKey = profile.Picks.GetValueOrDefault(variable);
        var picked = suppliers.FirstOrDefault(source => source.Key == pickedKey);
        if (!isRead) return Resolution("na", picked?.Key, []);
        if (picked is null) return Resolution("unresolved", pickedKey, []);
        string[] gaps = [.. RowsInScope(document, variable).Where(row => !Reaches(picked, row, reached))];
        if (gaps.Length > 0) return Resolution("gap", picked.Key, gaps);
        return Resolution(suppliers.Count > 1 ? "shadowed" : "ok", picked.Key, []);
    }

    /// <summary>Whether a source reaches a data row: a bridge source by a rule into it, a direct source by a value of its key.</summary>
    private static bool Reaches(ReportSource source, string row, IReadOnlyDictionary<string, IReadOnlySet<string>> reached)
    {
        if (source.RoleId is not null) return source.Rules.ContainsKey(row);
        return !reached.TryGetValue(source.Key, out var keys) || keys.Contains(row);
    }

    /// <summary>The data rows a variable reads: those its scope selects, or every one without a scope.</summary>
    private static IEnumerable<string> RowsInScope(ReportDocument document, string variable)
    {
        var parentOf = document.Rows.ToDictionary(row => row.Key, row => row.ParentKey, StringComparer.Ordinal);
        var data = document.Rows.Where(row => row.Kind == ReportRowKinds.Data).Select(row => row.Key);
        if (document.VariableScopes.FirstOrDefault(scope => scope.VariableKey == variable) is not { } scoped) return data;
        if (scoped.SelectionScope == SelectionScopes.Exact) return data.Where(row => row == scoped.ScopeMemberKey);
        return data.Where(row =>
        {
            for (string? current = row; current is not null; current = parentOf.GetValueOrDefault(current))
                if (current == scoped.ScopeMemberKey) return true;
            return false;
        });
    }

    /// <summary>A resolution.</summary>
    private static ReportVariableResolution Resolution(string status, string? sourceKey, IReadOnlyList<string> rowGaps)
    {
        return new ReportVariableResolution { Status = status, SourceKey = sourceKey, RowGaps = rowGaps };
    }

    /// <summary>The step of a rule's violation; a metric a column names belongs to the columns step.</summary>
    private static string StepOfViolation(RuleViolation violation)
    {
        return violation.Code == ReportMetricsResolveRule.Code && violation.Target?.Area == ReportAreas.Columns ? "columns" : StepOf[violation.Code];
    }

    /// <summary>A step with its findings: <c>fail</c> on a rule violation, <c>warn</c> on a coverage warning, <c>ok</c> otherwise.</summary>
    private static ReportDiagnosticsStep Step(string step, IReadOnlyList<RuleViolation> violations)
    {
        var status = violations.Count == 0 ? "ok" : step == "items" ? "warn" : "fail";
        return new ReportDiagnosticsStep { Step = step, Status = status, Violations = violations };
    }
}
