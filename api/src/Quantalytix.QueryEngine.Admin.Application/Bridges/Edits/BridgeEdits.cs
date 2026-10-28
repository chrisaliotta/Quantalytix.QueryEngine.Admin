using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The operations of the analytical bridge change set: the identity's display name and availability (key and endpoints are fixed),
/// the live version's settings, edited in place (OI-26), and its mappings. Whether the mappings comply is for the rules, which flag
/// them in the preview and refuse the commit (B11).
/// </summary>
internal static class BridgeEdits
{
    /// <summary>The kind of item the mapping operations name.</summary>
    private const string Mapping = "Mapping";

    /// <summary><c>updateBridge</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The aggregate with the patched identity.</returns>
    public static BridgeSnapshot Update(BridgeSnapshot snapshot, BridgeUpdateOperation operation)
    {
        var patch = operation.Patch;
        return snapshot with
        {
            Bridge = snapshot.Bridge with
            {
                DisplayName = patch.DisplayName ?? snapshot.Bridge.DisplayName,
                IsEnabled = patch.IsEnabled ?? snapshot.Bridge.IsEnabled
            }
        };
    }

    /// <summary><c>updateBridgeVersion</c>: the live version takes the new settings; its mappings stay.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The aggregate with the patched version.</returns>
    public static BridgeSnapshot UpdateVersion(BridgeSnapshot snapshot, BridgeVersionUpdateOperation operation)
    {
        return snapshot with { Version = snapshot.Version with { Cardinality = operation.Patch.Cardinality, AllocationMode = operation.Patch.AllocationMode } };
    }

    /// <summary><c>saveBridgeMapping</c>: adds the mapping, or replaces the mapping with the same id.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the mapping saved.</returns>
    public static BridgeSnapshot Save(BridgeSnapshot snapshot, BridgeMappingSaveOperation operation, ChangeContext context)
    {
        if (!LiveEditTargets.AllPresent([operation.Input.Id], StoredIds(snapshot), Mapping, context)) return snapshot;
        var saved = Row(snapshot, operation.Input, context);
        return snapshot with { Mappings = [.. snapshot.Mappings.Where(mapping => mapping.BridgeMappingId != saved.BridgeMappingId), saved] };
    }

    /// <summary><c>deleteBridgeMapping</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate without the mapping.</returns>
    public static BridgeSnapshot Delete(BridgeSnapshot snapshot, BridgeMappingDeleteOperation operation, ChangeContext context)
    {
        if (LiveEditTargets.Find(snapshot.Mappings, mapping => mapping.BridgeMappingId, operation.MappingId, Mapping, context) is not { } gone) return snapshot;
        return snapshot with { Mappings = [.. snapshot.Mappings.Where(mapping => mapping.BridgeMappingId != gone.BridgeMappingId)] };
    }

    /// <summary><c>replaceBridgeMappings</c>: the rows become the version's mappings; stored mappings they leave out are removed.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the mappings replaced.</returns>
    public static BridgeSnapshot Replace(BridgeSnapshot snapshot, BridgeMappingsReplaceOperation operation, ChangeContext context)
    {
        if (!LiveEditTargets.AllPresent(operation.Rows.Select(row => row.Id), StoredIds(snapshot), Mapping, context)) return snapshot;
        return snapshot with { Mappings = [.. operation.Rows.Select(row => Row(snapshot, row, context))] };
    }

    /// <summary>The ids of the version's mappings.</summary>
    private static HashSet<long> StoredIds(BridgeSnapshot snapshot)
    {
        return [.. snapshot.Mappings.Select(mapping => mapping.BridgeMappingId)];
    }

    /// <summary>The row of a mapping input in the live version; an absent source selection is <c>member</c>.</summary>
    private static BridgeMappingRecord Row(BridgeSnapshot snapshot, BridgeMappingInput input, ChangeContext context)
    {
        return new BridgeMappingRecord
        {
            BridgeMappingId = context.Ids.Resolve(input.Id),
            BridgeVersionId = snapshot.VersionId,
            SourceMemberKey = input.SourceMemberKey,
            TargetMemberKey = input.TargetMemberKey,
            Factor = input.Factor,
            AllocationWeight = input.AllocationWeight,
            SourceSelection = input.SourceSelection ?? SourceSelections.Member
        };
    }
}
