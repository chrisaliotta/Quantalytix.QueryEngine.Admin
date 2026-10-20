using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// The pins of assumption sets, one table per living kind: lists, writes and deletes a set's pins, and repoints the active set.
/// The pin tables key by set and identity, and StarRocks upserts into a primary key table, so pinning an identity again replaces
/// its pin.
/// </summary>
public sealed class AssumptionPins(
    ITableGateway<AssumptionSetRecord> sets,
    ITableGateway<AssumptionSetDimensionRecord> dimensions,
    ITableGateway<AssumptionSetMappingRecord> mappings,
    ITableGateway<AssumptionSetBridgeRecord> bridges,
    ITableGateway<AssumptionSetModelRecord> models,
    ITableGateway<AssumptionSetReportRecord> reports)
{
    /// <summary>The active set, which pins every published version.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The active set's row.</returns>
    public async Task<AssumptionSetRecord> ActiveAsync(CancellationToken cancellationToken)
    {
        return (await sets.ListAsync(new { Status = AssumptionSetStatuses.Active }, cancellationToken)).Single();
    }

    /// <summary>Pins a version in the active set, replacing its identity's pin.</summary>
    /// <param name="pin">The kind, identity and version just published.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>A task that completes when the pin is written.</returns>
    public async Task PinActiveAsync(PinnedVersion pin, CancellationToken cancellationToken)
    {
        var active = await ActiveAsync(cancellationToken);
        await InsertAsync(active.AssumptionSetId, [pin], cancellationToken);
    }

    /// <summary>Lists a set's pins of every kind.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pins, kind by kind.</returns>
    public async Task<IReadOnlyList<PinnedVersion>> ListAsync(long assumptionSetId, CancellationToken cancellationToken)
    {
        var filter = new { AssumptionSetId = assumptionSetId };
        return
        [
            .. (await dimensions.ListAsync(filter, cancellationToken)).Select(pin => new PinnedVersion(VersionKinds.DimensionVersion, pin.DimensionId, pin.DimensionVersionId)),
            .. (await mappings.ListAsync(filter, cancellationToken)).Select(pin => new PinnedVersion(VersionKinds.MappingVersion, pin.MappingSetId, pin.MappingVersionId)),
            .. (await bridges.ListAsync(filter, cancellationToken)).Select(pin => new PinnedVersion(VersionKinds.BridgeVersion, pin.BridgeId, pin.BridgeVersionId)),
            .. (await models.ListAsync(filter, cancellationToken)).Select(pin => new PinnedVersion(VersionKinds.CalculationModelVersion, pin.ModelId, pin.ModelVersionId)),
            .. (await reports.ListAsync(filter, cancellationToken)).Select(pin => new PinnedVersion(VersionKinds.ReportVersion, pin.ReportId, pin.ReportVersionId))
        ];
    }

    /// <summary>Writes pins into a set, replacing the pins of the same identities.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="pins">The pins to write.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when every pin is written.</returns>
    public async Task InsertAsync(long assumptionSetId, IReadOnlyCollection<PinnedVersion> pins, CancellationToken cancellationToken)
    {
        await dimensions.InsertManyAsync(
            [.. Of(pins, VersionKinds.DimensionVersion).Select(pin => new AssumptionSetDimensionRecord { AssumptionSetId = assumptionSetId, DimensionId = pin.IdentityId, DimensionVersionId = pin.VersionId })],
            cancellationToken);
        await mappings.InsertManyAsync(
            [.. Of(pins, VersionKinds.MappingVersion).Select(pin => new AssumptionSetMappingRecord { AssumptionSetId = assumptionSetId, MappingSetId = pin.IdentityId, MappingVersionId = pin.VersionId })],
            cancellationToken);
        await bridges.InsertManyAsync(
            [.. Of(pins, VersionKinds.BridgeVersion).Select(pin => new AssumptionSetBridgeRecord { AssumptionSetId = assumptionSetId, BridgeId = pin.IdentityId, BridgeVersionId = pin.VersionId })],
            cancellationToken);
        await models.InsertManyAsync(
            [.. Of(pins, VersionKinds.CalculationModelVersion).Select(pin => new AssumptionSetModelRecord { AssumptionSetId = assumptionSetId, ModelId = pin.IdentityId, ModelVersionId = pin.VersionId })],
            cancellationToken);
        await reports.InsertManyAsync(
            [.. Of(pins, VersionKinds.ReportVersion).Select(pin => new AssumptionSetReportRecord { AssumptionSetId = assumptionSetId, ReportId = pin.IdentityId, ReportVersionId = pin.VersionId })],
            cancellationToken);
    }

    /// <summary>Deletes every pin of a set.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the pins are gone.</returns>
    public async Task DeleteAsync(long assumptionSetId, CancellationToken cancellationToken)
    {
        var filter = new { AssumptionSetId = assumptionSetId };
        await dimensions.DeleteAsync(filter, cancellationToken);
        await mappings.DeleteAsync(filter, cancellationToken);
        await bridges.DeleteAsync(filter, cancellationToken);
        await models.DeleteAsync(filter, cancellationToken);
        await reports.DeleteAsync(filter, cancellationToken);
    }

    /// <summary>The pins of one kind.</summary>
    private static IEnumerable<PinnedVersion> Of(IEnumerable<PinnedVersion> pins, string kind)
    {
        return pins.Where(pin => pin.Kind == kind);
    }
}
