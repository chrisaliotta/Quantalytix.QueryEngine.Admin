using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// Lists and shows assumption sets, captures historical sets from the active set and the active release, relabels sets and
/// deletes historical ones. The active set is never created or deleted here: the tenant database holds it, and every publish
/// repoints it. Capturing or deleting a set changes which keys requests can name, so both advance the semantic revision.
/// </summary>
public sealed class AssumptionSetService(
    ITableGateway<AssumptionSetRecord> sets,
    AssumptionPins pins,
    AssumptionPinReader pinReader,
    ReportOwnership ownership,
    ITableGateway<DatabaseInfoRecord> databaseInfo,
    SemanticRevision semanticRevision,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>Lists every assumption set: the active one, then historical sets, most recently captured first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The assumption sets.</returns>
    public async Task<IReadOnlyList<AssumptionSetRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await sets.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.Status != AssumptionSetStatuses.Active).ThenByDescending(row => row.CapturedAt)];
    }

    /// <summary>Reads one assumption set with its pins.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The set and its labelled pins.</returns>
    public async Task<AssumptionSetDetail> GetAsync(long assumptionSetId, CancellationToken cancellationToken)
    {
        return new AssumptionSetDetail
        {
            Set = await sets.GetAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken),
            Pins = await pinReader.ListAsync(assumptionSetId, cancellationToken)
        };
    }

    /// <summary>
    /// Captures a historical set: a copy of the active set's pins under a new key, stamped with the capture time, that reads the
    /// active release's dataset versions from then on, so a backtest runs the pins over the datasets they were validated with.
    /// Reports always run on the active set, so the pins of report dimensions and of the bridges into them are not copied.
    /// </summary>
    /// <param name="input">The validated key and labels.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new historical set.</returns>
    public async Task<AssumptionSetRecord> CaptureAsync(AssumptionCaptureInput input, CancellationToken cancellationToken)
    {
        var active = await pins.ActiveAsync(cancellationToken);
        var info = await databaseInfo.GetAsync(new { SingletonId }, cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;
        var set = new AssumptionSetRecord
        {
            AssumptionSetId = ids.NextId(),
            AssumptionSetKey = input.AssumptionSetKey,
            DisplayName = input.DisplayName,
            Description = input.Description,
            Status = AssumptionSetStatuses.Historical,
            SemanticReleaseId = info.ActiveSemanticReleaseId,
            CapturedAt = now,
            CreatedAt = now
        };
        await sets.InsertAsync(set, cancellationToken);
        var (reportDimensions, reportBridges) = await ownership.OwnedIdsAsync(cancellationToken);
        PinnedVersion[] captured =
        [
            .. (await pins.ListAsync(active.AssumptionSetId, cancellationToken)).Where(pin =>
                !(pin.Kind == VersionKinds.DimensionVersion && reportDimensions.Contains(pin.IdentityId))
                && !(pin.Kind == VersionKinds.BridgeVersion && reportBridges.Contains(pin.IdentityId)))
        ];
        await pins.InsertAsync(set.AssumptionSetId, captured, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return set;
    }

    /// <summary>Changes a set's display name and description; its key and pins stay.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="input">The validated labels.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The updated set.</returns>
    public async Task<AssumptionSetRecord> UpdateAsync(long assumptionSetId, AssumptionSetUpdateInput input, CancellationToken cancellationToken)
    {
        var current = await sets.GetAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
        var updated = current with { DisplayName = input.DisplayName, Description = input.Description };
        await sets.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Deletes a historical set with its pins; the active set holds the default versions and cannot be deleted.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancels the read and the deletes.</param>
    /// <returns>Null when the set was deleted; otherwise the detail naming why not, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long assumptionSetId, CancellationToken cancellationToken)
    {
        var set = await sets.GetAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
        if (set.Status == AssumptionSetStatuses.Active)
            return $"Assumption set '{set.AssumptionSetKey}' is the active set, which holds the default versions; it cannot be deleted.";
        await pins.DeleteAsync(assumptionSetId, cancellationToken);
        await sets.DeleteAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }
}
