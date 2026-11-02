using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The report change-set aggregate (blueprint 16, OI-46): the live report version row, its rows (the report dimension's members,
/// member options and overlay lines), sources with their report bridges' rules and left-out items, profiles, calculation dimensions,
/// scenarios, scopes, columns and styles, saved together. <see cref="ReportGateRule"/> refuses a change that leaves an enabled report
/// breaking a rule. The commit writes the report's own rows first and the bridge sources' bridges and rules with the sources that own
/// them (<see cref="ReportSourceWrites"/>), the version row last.
/// </summary>
/// <param name="loader">Reads the report.</param>
/// <param name="versions">The report version table.</param>
/// <param name="members">The report dimension's member table.</param>
/// <param name="closure">The report dimension's closure table.</param>
/// <param name="lines">The overlay line table.</param>
/// <param name="options">The member option table.</param>
/// <param name="styles">The style table.</param>
/// <param name="columns">The column table.</param>
/// <param name="profiles">The profile table.</param>
/// <param name="picks">The profile pick table.</param>
/// <param name="dimensions">The calculation dimension table.</param>
/// <param name="scenarios">The scenario table.</param>
/// <param name="scopes">The variable scope table.</param>
/// <param name="rowParameters">The row parameter value table.</param>
/// <param name="sources">Plans the source rows with their report bridges and rules.</param>
/// <param name="sourceVariables">The source variable table.</param>
/// <param name="leftOut">The left-out item table.</param>
public sealed class ReportAggregate(
    ReportSnapshotLoader loader,
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<DimensionClosureRecord> closure,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportMemberOptionRecord> options,
    ITableGateway<ReportStyleRecord> styles,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<ReportScenarioRecord> scenarios,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<ReportRowParameterRecord> rowParameters,
    ReportSourceWrites sources,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<ReportSourceLeftOutRecord> leftOut)
    : IChangeSetAggregate<ReportSnapshot, ReportOperation, ReportDocument>
{
    /// <summary>The depth of the report's content rows below the report row.</summary>
    private const int ContentDepth = 1;

    /// <summary>The depth of the rows that hang off a source or profile: variables, picks, left-out items and bridge rules.</summary>
    private const int DependentDepth = 2;

    /// <summary>The depth of the report dimension's closure, derived from its members.</summary>
    private const int ClosureDepth = 3;

    /// <summary>Reads the report's live version.</summary>
    /// <param name="aggregateId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public Task<ReportSnapshot> LoadAsync(long aggregateId, CancellationToken cancellationToken)
    {
        return loader.LoadAsync(aggregateId, cancellationToken);
    }

    /// <summary>Applies one operation.</summary>
    /// <param name="snapshot">The report so far.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Allocates ids and takes refusals.</param>
    /// <returns>The report with the operation applied.</returns>
    public ReportSnapshot Apply(ReportSnapshot snapshot, ReportOperation operation, ChangeContext context)
    {
        return ReportEdits.Apply(snapshot, operation, context);
    }

    /// <summary>Plans the row writes: content, then what hangs off sources and profiles, then the closure, the version row last.</summary>
    /// <param name="before">The report as stored.</param>
    /// <param name="after">The report with every operation applied.</param>
    /// <param name="plan">The plan.</param>
    public void Plan(ReportSnapshot before, ReportSnapshot after, WritePlan plan)
    {
        var stored = before.Content;
        var changed = after.Content;
        if (stored.Version != changed.Version) plan.Update(versions, WritePlan.Root, changed.Version);
        plan.Sync(members, ContentDepth, stored.Members, changed.Members, row => new { row.DimensionMemberId });
        plan.Sync(lines, ContentDepth, stored.Lines, changed.Lines, row => new { row.ReportLineId });
        plan.Sync(options, ContentDepth, stored.MemberOptions, changed.MemberOptions, row => new { row.ReportVersionId, row.MemberKey });
        plan.Sync(styles, ContentDepth, stored.Styles, changed.Styles, row => new { row.ReportVersionId, row.StyleKey });
        plan.Sync(columns, ContentDepth, stored.Columns, changed.Columns, row => new { row.ReportColumnId });
        plan.Sync(profiles, ContentDepth, stored.Profiles, changed.Profiles, row => new { row.CalculationSourceProfileId });
        plan.Sync(dimensions, ContentDepth, stored.Dimensions, changed.Dimensions, row => new { row.CalculationDimensionId });
        plan.Sync(scenarios, ContentDepth, stored.Scenarios, changed.Scenarios, row => new { row.ReportVersionId, row.DimensionRoleId });
        plan.Sync(scopes, ContentDepth, stored.Scopes, changed.Scopes, row => new { row.ReportVersionId, row.VariableKey });
        plan.Sync(rowParameters, ContentDepth, stored.RowParameters, changed.RowParameters, row => new { row.ReportVersionId, row.RowKey, row.ParameterKey });
        sources.Plan(before, after, ContentDepth, plan);
        plan.Sync(sourceVariables, DependentDepth, stored.SourceVariables, changed.SourceVariables, row => new { row.ReportSourceId, row.VariableKey });
        plan.Sync(picks, DependentDepth, stored.Picks, changed.Picks, row => new { row.CalculationSourceProfileId, row.VariableKey });
        plan.Sync(leftOut, DependentDepth, stored.LeftOut, changed.LeftOut, row => new { row.ReportSourceId, row.MemberKey });
        if (Same(stored.Members, changed.Members)) return;
        plan.Delete(closure, ClosureDepth, new { after.Surroundings.DimensionVersionId });
        plan.Insert(closure, ClosureDepth, ClosureBuilder.ClosureOf(after.Surroundings.DimensionVersionId, changed.Members));
    }

    /// <summary>The document of a snapshot; the report change set allocates no temporary ids, so every id is stored.</summary>
    /// <param name="snapshot">The report.</param>
    /// <param name="presentId">Unused: report content is named by key.</param>
    /// <param name="cancellationToken">Unused; the snapshot holds everything.</param>
    /// <returns>The document.</returns>
    public Task<ReportDocument> ReadAsync(ReportSnapshot snapshot, Func<long, string> presentId, CancellationToken cancellationToken)
    {
        return Task.FromResult(ReportReadModels.Document(snapshot));
    }

    /// <summary>Whether two row lists hold the same rows, in any order.</summary>
    private static bool Same<TRecord>(IReadOnlyList<TRecord> before, IReadOnlyList<TRecord> after)
    {
        return before.Count == after.Count && before.ToHashSet().SetEquals(after);
    }
}
