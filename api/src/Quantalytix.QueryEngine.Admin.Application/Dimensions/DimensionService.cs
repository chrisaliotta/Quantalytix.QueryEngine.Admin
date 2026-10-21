using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Reads, creates, edits and deletes canonical dimensions: the identity rows that own the dimension versions.</summary>
public sealed class DimensionService(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionRoleRecord> roles,
    DimensionVersionService dimensionVersions,
    DimensionRoleService dimensionRoles,
    DimensionMemberService members,
    DimensionVersionRemover remover,
    DimensionDeleteChecks deleteChecks,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>The key of the single root row a new report dimension starts with.</summary>
    public const string ReportRootKey = "total";

    /// <summary>Lists every dimension.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The dimensions by key, ignoring case.</returns>
    public async Task<IReadOnlyList<DimensionRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await dimensions.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.DimensionKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one dimension.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The dimension.</returns>
    public Task<DimensionRecord> GetAsync(long dimensionId, CancellationToken cancellationToken)
    {
        return dimensions.GetAsync(new { DimensionId = dimensionId }, cancellationToken);
    }

    /// <summary>
    /// Creates a user-defined analytical dimension, its draft version 1 and, unless it is a time dimension, its first role.
    /// Report dimensions are created with their report, through <see cref="CreateReportDimensionAsync"/>.
    /// </summary>
    /// <param name="input">The new dimension, already validated.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored dimension.</returns>
    public async Task<DimensionRecord> CreateAsync(NewDimensionInput input, CancellationToken cancellationToken)
    {
        var dimension = new DimensionRecord
        {
            DimensionId = ids.NextId(),
            DimensionKey = input.DimensionKey,
            DisplayName = input.DisplayName,
            DimensionKind = input.DimensionKind,
            DefinitionOrigin = DefinitionOrigins.User,
            IsRemovable = input.IsRemovable,
            IsEnabled = input.IsEnabled,
            DimensionPurpose = DimensionPurposes.Analytical
        };
        await dimensions.InsertAsync(dimension, cancellationToken);
        var firstVersion = new NewDimensionVersionInput { RangeDataType = input.RangeDataType };
        await dimensionVersions.CreateAsync(dimension.DimensionId, firstVersion, cancellationToken);
        if (input.DefaultRole is { } role) await dimensionRoles.CreateAsync(dimension.DimensionId, role, cancellationToken);
        return dimension;
    }

    /// <summary>
    /// Creates the report dimension of a new report: a hierarchical dimension of purpose <c>report</c> without roles, keyed and
    /// named like the report, whose draft version 1 holds a copy of the template version's levels and members, or else a
    /// single root row named after the report. Validation rebuilds a copy's closure.
    /// </summary>
    /// <param name="key">The report's key, which the dimension takes.</param>
    /// <param name="displayName">The report's name, which the dimension and its root row take.</param>
    /// <param name="templateVersionId">The published version of the dimension the report starts from, or null for a single root row.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored dimension.</returns>
    public async Task<DimensionRecord> CreateReportDimensionAsync(string key, string displayName, long? templateVersionId, CancellationToken cancellationToken)
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
        var firstVersion = await dimensionVersions.CreateAsync(
            dimension.DimensionId, new NewDimensionVersionInput { CloneFromVersionId = templateVersionId }, cancellationToken);
        if (templateVersionId is null)
        {
            var root = new NewMemberInput { MemberKey = ReportRootKey, DisplayName = displayName, IsAssignable = false, SortOrder = 0 };
            await members.CreateAsync(firstVersion.DimensionVersionId, root, cancellationToken);
        }
        return dimension;
    }

    /// <summary>
    /// Edits a dimension's identity attributes. A <c>system</c> dimension keeps its stored <c>is_removable</c>, because platform
    /// fixtures must never become deletable. A changed display name or availability advances the semantic revision, because
    /// the query engine caches both.
    /// </summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The stored dimension.</returns>
    public async Task<DimensionRecord> UpdateAsync(long dimensionId, DimensionInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(dimensionId, cancellationToken);
        var updated = current with
        {
            DisplayName = input.DisplayName,
            IsEnabled = input.IsEnabled,
            IsRemovable = RemovabilityAfter(current, input)
        };
        await dimensions.UpdateAsync(updated, cancellationToken);
        if (updated.DisplayName != current.DisplayName || updated.IsEnabled != current.IsEnabled) await semanticRevision.AdvanceAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a removable dimension whose versions are all drafts and that nothing references: its drafts with their
    /// content first, then its roles, which nothing names, then the dimension.
    /// </summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the dimension was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var dimension = await GetAsync(dimensionId, cancellationToken);
        var ownVersions = await versions.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        var conflict = await deleteChecks.DimensionAsync(dimension, ownVersions, cancellationToken);
        if (conflict is not null) return conflict;
        await RemoveAsync(dimensionId, ownVersions, cancellationToken);
        return null;
    }

    /// <summary>
    /// Deletes a dimension whose versions are all drafts and that nothing references any more: its drafts with their content,
    /// then its roles, then the dimension. Its report deletes a report dimension this way, once the report and its bridges are gone.
    /// </summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="ownVersions">Every version of the dimension.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the dimension is gone.</returns>
    public async Task RemoveAsync(long dimensionId, IReadOnlyList<DimensionVersionRecord> ownVersions, CancellationToken cancellationToken)
    {
        await remover.RemoveAsync([.. ownVersions.Select(version => version.DimensionVersionId)], cancellationToken);
        await roles.DeleteAsync(new { DimensionId = dimensionId }, cancellationToken);
        await dimensions.DeleteAsync(new { DimensionId = dimensionId }, cancellationToken);
    }

    /// <summary>The removability an update stores: the input's, except that a system dimension keeps its own.</summary>
    private static bool RemovabilityAfter(DimensionRecord current, DimensionInput input)
    {
        if (current.DefinitionOrigin == DefinitionOrigins.System) return current.IsRemovable;
        return input.IsRemovable;
    }
}
