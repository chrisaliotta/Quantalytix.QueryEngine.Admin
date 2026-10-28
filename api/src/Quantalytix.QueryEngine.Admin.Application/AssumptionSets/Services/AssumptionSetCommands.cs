using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// The assumption set commands (R8–R11): capture a historical set, relabel it and delete it. The active set is never created,
/// relabelled or deleted here: the tenant database holds it, and every publish repoints it. Capturing or deleting a set changes which
/// keys requests can name, so both advance the semantic revision; relabelling does not.
/// </summary>
public sealed class AssumptionSetCommands(
    ITableGateway<AssumptionSetRecord> sets,
    AssumptionPins pins,
    PinIdentityReader identities,
    TenantInfoReader tenant,
    SemanticRevision semanticRevision,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>
    /// Captures the active set's analytical pins and the active release under a new key (R9): reports always run on the active set,
    /// so report dimensions and the bridges into them are left out. The pins are written before the set row, so a stop leaves pins no
    /// request can name rather than a set missing pins.
    /// </summary>
    /// <param name="input">The validated key and labels.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new set, or a conflict when the key is taken.</returns>
    public async Task<Outcome<AssumptionSet>> CaptureAsync(CaptureSetInput input, CancellationToken cancellationToken)
    {
        if ((await sets.ListAsync(new { AssumptionSetKey = input.Key }, cancellationToken)).Count > 0)
            return Outcome<AssumptionSet>.Refused(Refusal.Conflict("Key taken", $"Another assumption set already uses the key '{input.Key}'."));

        var index = await identities.ReadAsync(cancellationToken);
        var active = await pins.ActiveAsync(cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;
        var set = new AssumptionSetRecord
        {
            AssumptionSetId = ids.NextId(),
            AssumptionSetKey = input.Key,
            DisplayName = input.DisplayName.Trim(),
            Description = input.Description,
            Status = AssumptionSetStatuses.Historical,
            SemanticReleaseId = (await tenant.ReadAsync(cancellationToken)).ActiveReleaseId,
            CapturedAt = now,
            CreatedAt = now
        };
        await pins.InsertAsync(set.AssumptionSetId, [.. (await pins.ListAsync(active.AssumptionSetId, cancellationToken)).Where(pin => !index.IsReportOwned(pin))], cancellationToken);
        await sets.InsertAsync(set, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<AssumptionSet>.Success(new AssumptionSet(set));
    }

    /// <summary>Changes a historical set's display name and description; its key, pins and release stay (R10).</summary>
    /// <param name="setId">The set.</param>
    /// <param name="input">The validated labels.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The relabelled set, or a conflict for the active set.</returns>
    public async Task<Outcome<AssumptionSet>> RelabelAsync(long setId, RelabelSetInput input, CancellationToken cancellationToken)
    {
        var set = await sets.GetAsync(new { AssumptionSetId = setId }, cancellationToken);
        if (set.Status == AssumptionSetStatuses.Active) return Outcome<AssumptionSet>.Refused(ActiveSet(set, "relabelled"));
        var relabelled = set with { DisplayName = input.DisplayName.Trim(), Description = input.Description };
        await sets.UpdateAsync(relabelled, cancellationToken);
        return Outcome<AssumptionSet>.Success(new AssumptionSet(relabelled));
    }

    /// <summary>
    /// Deletes a historical set with its pins (R10). The set row goes first, so a stop leaves pins no request can name rather than a
    /// set missing pins.
    /// </summary>
    /// <param name="setId">The set.</param>
    /// <param name="cancellationToken">Cancels the read and the deletes.</param>
    /// <returns>Null when deleted; a conflict for the active set.</returns>
    public async Task<Refusal?> DeleteAsync(long setId, CancellationToken cancellationToken)
    {
        var set = await sets.GetAsync(new { AssumptionSetId = setId }, cancellationToken);
        if (set.Status == AssumptionSetStatuses.Active) return ActiveSet(set, "deleted");
        await sets.DeleteAsync(new { AssumptionSetId = setId }, cancellationToken);
        await pins.DeleteAsync(setId, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }

    /// <summary>The conflict of a command the active set refuses.</summary>
    private static Refusal ActiveSet(AssumptionSetRecord set, string done)
    {
        return Refusal.Conflict("The active set", $"Assumption set '{set.AssumptionSetKey}' is the active set, which holds the default versions; it cannot be {done}.");
    }
}
