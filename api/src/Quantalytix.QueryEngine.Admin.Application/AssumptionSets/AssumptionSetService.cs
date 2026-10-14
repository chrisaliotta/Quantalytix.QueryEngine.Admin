using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// Creates, edits and deletes assumption sets and their pinned dimension versions (<c>qe_assumption_set</c>,
/// <c>qe_assumption_set_dimension</c>), and captures the versions effective at a set's <c>effective_at</c>.
/// </summary>
public sealed class AssumptionSetService(
    ITableGateway<AssumptionSetRecord> sets,
    ITableGateway<AssumptionSetDimensionRecord> pins,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>Lists every assumption set, ordered by key.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The assumption sets.</returns>
    public async Task<IReadOnlyList<AssumptionSetRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await sets.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.AssumptionSetKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one assumption set.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The assumption set row.</returns>
    public Task<AssumptionSetRecord> GetAsync(long assumptionSetId, CancellationToken cancellationToken)
    {
        return sets.GetAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
    }

    /// <summary>Creates a draft assumption set without pins.</summary>
    /// <param name="input">The validated key and settings.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new assumption set row.</returns>
    public async Task<AssumptionSetRecord> CreateAsync(AssumptionSetCreateInput input, CancellationToken cancellationToken)
    {
        var set = new AssumptionSetRecord
        {
            AssumptionSetId = ids.NextId(),
            AssumptionSetKey = input.AssumptionSetKey,
            DisplayName = input.DisplayName,
            Description = input.Description,
            EffectiveAt = input.EffectiveAt,
            Status = AssumptionSetStatuses.Draft,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await sets.InsertAsync(set, cancellationToken);
        return set;
    }

    /// <summary>Changes a draft assumption set's display name, description and effective time.</summary>
    /// <param name="assumptionSetId">The draft assumption set.</param>
    /// <param name="input">The validated settings.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The updated assumption set row.</returns>
    public async Task<AssumptionSetRecord> UpdateAsync(long assumptionSetId, AssumptionSetUpdateInput input, CancellationToken cancellationToken)
    {
        var current = await sets.GetAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
        var updated = current with { DisplayName = input.DisplayName, Description = input.Description, EffectiveAt = input.EffectiveAt };
        await sets.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Deletes a draft assumption set: its pins first, then the set.</summary>
    /// <param name="assumptionSetId">The draft assumption set.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the set is gone.</returns>
    public async Task DeleteAsync(long assumptionSetId, CancellationToken cancellationToken)
    {
        await pins.DeleteAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
        await sets.DeleteAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
    }

    /// <summary>Lists an assumption set's pins, ordered by dimension.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pins.</returns>
    public async Task<IReadOnlyList<AssumptionSetDimensionRecord>> ListPinsAsync(long assumptionSetId, CancellationToken cancellationToken)
    {
        var rows = await pins.ListAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
        return [.. rows.OrderBy(row => row.DimensionId)];
    }

    /// <summary>Replaces a draft assumption set's pins with the given ones.</summary>
    /// <param name="assumptionSetId">The draft assumption set.</param>
    /// <param name="input">The validated pins.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored pins, ordered by dimension.</returns>
    public async Task<IReadOnlyList<AssumptionSetDimensionRecord>> ReplacePinsAsync(long assumptionSetId, AssumptionPinsInput input, CancellationToken cancellationToken)
    {
        AssumptionSetDimensionRecord[] rows =
        [
            .. input.Dimensions
                .OrderBy(pin => pin.DimensionId)
                .Select(pin => new AssumptionSetDimensionRecord
                {
                    AssumptionSetId = assumptionSetId,
                    DimensionId = pin.DimensionId,
                    DimensionVersionId = pin.DimensionVersionId
                })
        ];
        await pins.DeleteAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
        await pins.InsertManyAsync(rows, cancellationToken);
        return rows;
    }

    /// <summary>
    /// Replaces the pins of the given dimensions with the published version of each that is effective at the set's
    /// <c>effective_at</c>: <c>valid_from &lt;= effective_at &lt; valid_to</c>, where a null <c>valid_to</c> is open-ended. A
    /// dimension with no such version is left unpinned. Pins of other dimensions are kept.
    /// </summary>
    /// <param name="assumptionSetId">The draft assumption set.</param>
    /// <param name="input">The validated dimensions to capture.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>All of the set's pins after the capture, ordered by dimension.</returns>
    public async Task<IReadOnlyList<AssumptionSetDimensionRecord>> CaptureAsync(long assumptionSetId, AssumptionCaptureInput input, CancellationToken cancellationToken)
    {
        var set = await sets.GetAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
        var versions = await dimensionVersions.ListAsync(new { DimensionId = input.DimensionIds }, cancellationToken);
        AssumptionSetDimensionRecord[] captured =
        [
            .. versions
                .Where(version => version.Status == PublicationStatuses.Published && IsEffectiveAt(version, set.EffectiveAt))
                .Select(version => new AssumptionSetDimensionRecord
                {
                    AssumptionSetId = assumptionSetId,
                    DimensionId = version.DimensionId,
                    DimensionVersionId = version.DimensionVersionId
                })
        ];
        await pins.DeleteAsync(new { AssumptionSetId = assumptionSetId, DimensionId = input.DimensionIds }, cancellationToken);
        await pins.InsertManyAsync(captured, cancellationToken);
        return await ListPinsAsync(assumptionSetId, cancellationToken);
    }

    /// <summary>Whether a version's half-open validity window <c>[valid_from, valid_to)</c> contains an instant; a null <c>valid_to</c> never ends.</summary>
    private static bool IsEffectiveAt(DimensionVersionRecord version, DateTime instant)
    {
        return version.ValidFrom <= instant && (version.ValidTo is null || instant < version.ValidTo);
    }
}
