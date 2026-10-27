using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The dimensions module's hook for the reports module: creates and removes the report dimension whose members are one report's
/// rows. This module refuses every write to such a dimension and lists none (M8).
/// </summary>
public sealed class ReportDimensions(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionMemberRecord> members,
    DimensionDrafts drafts,
    DimensionVersionRemover remover,
    IIdGenerator ids)
{
    /// <summary>The key of the single root row a new report dimension starts with.</summary>
    public const string RootKey = "total";

    /// <summary>
    /// Creates the report dimension of a new report: hierarchical, purpose <c>report</c>, without roles, keyed and named like the
    /// report, whose draft version 1 holds a copy of the template version's levels and members, or else a single root row named
    /// after the report. Validation rebuilds the closure.
    /// </summary>
    /// <param name="key">The report's key, which the dimension takes.</param>
    /// <param name="displayName">The report's name, which the dimension and its root row take.</param>
    /// <param name="templateVersionId">The published version of the dimension the report starts from, or null for a single root row.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The dimension's draft version 1.</returns>
    public async Task<DimensionVersionRecord> CreateAsync(string key, string displayName, long? templateVersionId, CancellationToken cancellationToken)
    {
        var dimension = new DimensionRecord
        {
            DimensionId = ids.NextId(),
            DimensionKey = key,
            DisplayName = displayName,
            DimensionKind = DimensionKinds.Hierarchical,
            DefinitionOrigin = DefinitionOrigins.User,
            IsRemovable = true,
            IsEnabled = true,
            DimensionPurpose = DimensionPurposes.Report
        };
        await dimensions.InsertAsync(dimension, cancellationToken);
        if (templateVersionId is { } sourceId) return await drafts.CloneAsync(dimension.DimensionId, sourceId, cancellationToken);

        var draft = await drafts.CreateAsync(dimension.DimensionId, null, cancellationToken);
        await members.InsertAsync(
            new DimensionMemberRecord
            {
                DimensionMemberId = ids.NextId(),
                DimensionVersionId = draft.DimensionVersionId,
                MemberKey = RootKey,
                DisplayName = displayName,
                IsAssignable = false,
                SortOrder = 0
            },
            cancellationToken);
        return draft;
    }

    /// <summary>
    /// Deletes a report dimension that nothing references any more, once its report's content, bridges and pins are gone: its
    /// versions with their content, then the dimension.
    /// </summary>
    /// <param name="dimensionId">The report dimension.</param>
    /// <param name="ownVersions">Every version of the dimension.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the dimension is gone.</returns>
    public async Task RemoveAsync(long dimensionId, IReadOnlyList<DimensionVersionRecord> ownVersions, CancellationToken cancellationToken)
    {
        await remover.RemoveAsync([.. ownVersions.Select(version => version.DimensionVersionId)], cancellationToken);
        await roles.DeleteAsync(new { DimensionId = dimensionId }, cancellationToken);
        await dimensions.DeleteAsync(new { DimensionId = dimensionId }, cancellationToken);
    }
}
