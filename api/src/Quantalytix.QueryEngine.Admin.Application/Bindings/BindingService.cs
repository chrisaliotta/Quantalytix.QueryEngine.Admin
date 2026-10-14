using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Lists, creates, replaces and deletes the dimension bindings of dataset versions together with the time extensions of
/// their time bindings. The binding row is written before its extension and deleted after it.
/// </summary>
public sealed class BindingService(
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<MeasureTimePolicyRecord> timePolicies,
    ITableGateway<MeasureRecord> measures,
    IIdGenerator ids)
{
    /// <summary>Lists the bindings of a dataset version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The bindings sorted by display name, each with its time extension and the bridge of its bridge version.</returns>
    public async Task<IReadOnlyList<BindingDetail>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var extensions = (await timeBindings.ListAsync(new { BindingId = rows.Select(row => row.BindingId).ToArray() }, cancellationToken))
            .ToDictionary(extension => extension.BindingId);
        var bridgeIds = await BridgeIdsAsync(rows, cancellationToken);
        return
        [
            .. rows
                .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.BindingId)
                .Select(row => Detail(row, extensions.GetValueOrDefault(row.BindingId), bridgeIds))
        ];
    }

    /// <summary>Creates a binding in a draft dataset version: the binding first, then the time extension of a time binding.</summary>
    /// <param name="versionId">The draft version.</param>
    /// <param name="input">The new binding.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The new binding.</returns>
    public async Task<BindingDetail> CreateAsync(long versionId, BindingInput input, CancellationToken cancellationToken)
    {
        var binding = ToRecord(ids.NextId(), versionId, input);
        await bindings.InsertAsync(binding, cancellationToken);
        return await SaveExtensionAsync(binding, input, cancellationToken);
    }

    /// <summary>
    /// Replaces a binding's attributes, then its time extension: a time binding writes it, any other kind deletes it, so a
    /// binding that stops being a time binding loses it.
    /// </summary>
    /// <param name="reference">The binding and its draft version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The updated binding.</returns>
    public async Task<BindingDetail> UpdateAsync(BindingReference reference, BindingInput input, CancellationToken cancellationToken)
    {
        var binding = ToRecord(reference.BindingId, reference.VersionId, input);
        await bindings.UpdateAsync(binding, cancellationToken);
        return await SaveExtensionAsync(binding, input, cancellationToken);
    }

    /// <summary>
    /// Deletes a binding, its time extension first, unless it is required, another binding remaps it through a bridge, a
    /// measure's time policy requires it, or it is the version's default time binding.
    /// </summary>
    /// <param name="reference">The binding and its draft version.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the binding was deleted; otherwise the detail naming everything that blocks the delete.</returns>
    public async Task<string?> DeleteAsync(BindingReference reference, CancellationToken cancellationToken)
    {
        var binding = await bindings.GetAsync(new { reference.BindingId }, cancellationToken);
        var version = await versions.GetAsync(new { DatasetVersionId = reference.VersionId }, cancellationToken);
        var bridged = await bindings.ListAsync(new { SourceBindingId = reference.BindingId }, cancellationToken);
        var policies = await timePolicies.ListAsync(new { RequiredTimeBindingId = reference.BindingId }, cancellationToken);
        var requiring = await measures.ListAsync(new { MeasureId = policies.Select(policy => policy.MeasureId).ToArray() }, cancellationToken);

        var blockers = new DeleteBlockers($"Binding '{binding.DisplayName}'");
        if (binding.IsRequired) blockers.Add("it is a required binding of the dataset");
        blockers.AddNamed("it is the source binding of bridge bindings", bridged.Select(row => $"'{row.DisplayName}'"));
        blockers.AddNamed("it is the required time binding of measures", requiring.Select(measure => $"'{measure.MeasureKey}'"));
        if (version.DefaultTimeBindingId == reference.BindingId) blockers.Add("it is the version's default time binding");
        if (blockers.Detail() is { } detail) return detail;

        await timeBindings.DeleteAsync(new { reference.BindingId }, cancellationToken);
        await bindings.DeleteAsync(new { reference.BindingId }, cancellationToken);
        return null;
    }

    /// <summary>Writes the time extension of a time binding, or deletes the extension of any other kind.</summary>
    private async Task<BindingDetail> SaveExtensionAsync(DimensionBindingRecord binding, BindingInput input, CancellationToken cancellationToken)
    {
        TimeBindingRecord? extension = null;
        if (input.TimeBinding is { } timeBinding)
        {
            extension = ToRecord(binding.BindingId, timeBinding);
            await timeBindings.InsertAsync(extension, cancellationToken);
        }
        else
        {
            await timeBindings.DeleteAsync(new { binding.BindingId }, cancellationToken);
        }

        return Detail(binding, extension, await BridgeIdsAsync([binding], cancellationToken));
    }

    /// <summary>The bridges that own the bridge versions of bridge bindings, by bridge version.</summary>
    private async Task<Dictionary<long, long>> BridgeIdsAsync(IReadOnlyList<DimensionBindingRecord> rows, CancellationToken cancellationToken)
    {
        long[] bridgeVersionIds = [.. rows.Where(row => row.BridgeVersionId is not null).Select(row => row.BridgeVersionId!.Value)];
        var owners = await bridgeVersions.ListAsync(new { BridgeVersionId = bridgeVersionIds }, cancellationToken);
        return owners.ToDictionary(owner => owner.BridgeVersionId, owner => owner.BridgeId);
    }

    /// <summary>A binding with its time extension and the bridge of its bridge version.</summary>
    private static BindingDetail Detail(DimensionBindingRecord binding, TimeBindingRecord? extension, Dictionary<long, long> bridgeIds)
    {
        long? bridgeId = null;
        if (binding.BridgeVersionId is { } bridgeVersionId && bridgeIds.TryGetValue(bridgeVersionId, out var owner)) bridgeId = owner;
        return new BindingDetail { Binding = binding, TimeBinding = extension, BridgeId = bridgeId };
    }

    /// <summary>A binding row holding the input's attributes.</summary>
    private static DimensionBindingRecord ToRecord(long bindingId, long versionId, BindingInput input)
    {
        return new DimensionBindingRecord
        {
            BindingId = bindingId,
            DatasetVersionId = versionId,
            DisplayName = input.DisplayName,
            DimensionId = input.DimensionId,
            PinnedDimensionVersionId = input.PinnedDimensionVersionId,
            BindingKind = input.BindingKind,
            SourceFieldId = input.SourceFieldId,
            MappingSetId = input.MappingSetId,
            SourceBindingId = input.SourceBindingId,
            BridgeVersionId = input.BridgeVersionId,
            GroupingMode = input.GroupingMode,
            GroupingLevelId = input.GroupingLevelId,
            GroupingAncestorMemberId = input.GroupingAncestorMemberId,
            GroupingDepth = input.GroupingDepth,
            RaggedBehavior = input.RaggedBehavior,
            DefaultPeriodAlignment = input.DefaultPeriodAlignment,
            UnmappedBehavior = input.UnmappedBehavior,
            UnknownMemberId = input.UnknownMemberId,
            IsRequired = input.IsRequired,
            IsGroupable = input.IsGroupable,
            IsFilterable = input.IsFilterable,
            IsEnabled = input.IsEnabled
        };
    }

    /// <summary>A time extension row holding the input's attributes; the allowed periodicities become a JSON array.</summary>
    private static TimeBindingRecord ToRecord(long bindingId, TimeBindingInput input)
    {
        return new TimeBindingRecord
        {
            BindingId = bindingId,
            TimeValueType = input.TimeValueType,
            ObservationKind = input.ObservationKind,
            SourceTimeZone = input.SourceTimeZone,
            NativePeriodicity = input.NativePeriodicity,
            IntervalEndFieldId = input.IntervalEndFieldId,
            AllowedPeriodicitiesJson = JsonSerializer.Serialize(input.AllowedPeriodicities),
            DefaultQueryTimeZone = input.DefaultQueryTimeZone,
            CumulativeOrigin = input.CumulativeOrigin,
            CumulativeFixedStart = input.CumulativeFixedStart
        };
    }
}
