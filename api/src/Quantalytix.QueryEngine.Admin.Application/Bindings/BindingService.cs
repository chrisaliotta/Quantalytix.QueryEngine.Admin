using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Lists, creates, replaces and deletes the dimension bindings of dataset versions together with the time extensions of
/// their time bindings. The binding row is written before its extension and deleted after it. A binding of a role holds the
/// role's dimension, which never changes, so every rule that compares dimensions reads it from the binding.
/// </summary>
public sealed class BindingService(
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<MeasureTimePolicyRecord> timePolicies,
    ITableGateway<MeasureRecord> measures,
    BindingNames names,
    IIdGenerator ids)
{
    /// <summary>Lists the bindings of a dataset version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The bindings sorted by role key, time bindings first, each with its role and time extension.</returns>
    public async Task<IReadOnlyList<BindingDetail>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var extensions = (await timeBindings.ListAsync(new { DimensionBindingId = rows.Select(row => row.DimensionBindingId).ToArray() }, cancellationToken))
            .ToDictionary(extension => extension.DimensionBindingId);
        var bound = (await roles.ListAsync(new { DimensionRoleId = rows.Select(row => row.DimensionRoleId).OfType<long>().Distinct().ToArray() }, cancellationToken))
            .ToDictionary(role => role.DimensionRoleId);
        return
        [
            .. rows
                .Select(row => new BindingDetail
                {
                    Binding = row,
                    Role = row.DimensionRoleId is { } dimensionRoleId ? bound[dimensionRoleId] : null,
                    TimeBinding = extensions.GetValueOrDefault(row.DimensionBindingId)
                })
                .OrderBy(detail => detail.Role?.DimensionRoleKey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(detail => detail.Binding.DimensionBindingId)
        ];
    }

    /// <summary>Creates a binding in a draft dataset version: the binding first, then the time extension of a time binding.</summary>
    /// <param name="versionId">The draft version.</param>
    /// <param name="input">The new binding.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The new binding.</returns>
    public async Task<BindingDetail> CreateAsync(long versionId, BindingInput input, CancellationToken cancellationToken)
    {
        var (binding, role) = await ToRecordAsync(ids.NextId(), versionId, input, cancellationToken);
        await bindings.InsertAsync(binding, cancellationToken);
        return await SaveExtensionAsync(binding, role, input, cancellationToken);
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
        var (binding, role) = await ToRecordAsync(reference.DimensionBindingId, reference.VersionId, input, cancellationToken);
        await bindings.UpdateAsync(binding, cancellationToken);
        return await SaveExtensionAsync(binding, role, input, cancellationToken);
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
        var binding = await bindings.GetAsync(new { reference.DimensionBindingId }, cancellationToken);
        var version = await versions.GetAsync(new { DatasetVersionId = reference.VersionId }, cancellationToken);
        var bridged = await bindings.ListAsync(new { SourceDimensionBindingId = reference.DimensionBindingId }, cancellationToken);
        var policies = await timePolicies.ListAsync(new { RequiredTimeBindingId = reference.DimensionBindingId }, cancellationToken);
        var requiring = await measures.ListAsync(new { MeasureId = policies.Select(policy => policy.MeasureId).ToArray() }, cancellationToken);
        var named = await names.OfAsync([binding, .. bridged], cancellationToken);

        var blockers = new DeleteBlockers($"Binding '{named[binding.DimensionBindingId]}'");
        if (binding.IsRequired) blockers.Add("it is a required binding of the dataset");
        blockers.AddNamed("it is the source binding of bridge bindings", bridged.Select(row => $"'{named[row.DimensionBindingId]}'"));
        blockers.AddNamed("it is the required time binding of measures", requiring.Select(measure => $"'{measure.MeasureKey}'"));
        if (version.DefaultTimeBindingId == reference.DimensionBindingId) blockers.Add("it is the version's default time binding");
        if (blockers.Detail() is { } detail) return detail;

        await timeBindings.DeleteAsync(new { reference.DimensionBindingId }, cancellationToken);
        await bindings.DeleteAsync(new { reference.DimensionBindingId }, cancellationToken);
        return null;
    }

    /// <summary>Writes the time extension of a time binding, or deletes the extension of any other kind.</summary>
    private async Task<BindingDetail> SaveExtensionAsync(
        DimensionBindingRecord binding, DimensionRoleRecord? role, BindingInput input, CancellationToken cancellationToken)
    {
        TimeBindingRecord? extension = null;
        if (input.TimeBinding is { } timeBinding)
        {
            extension = ToRecord(binding.DimensionBindingId, timeBinding);
            await timeBindings.InsertAsync(extension, cancellationToken);
        }
        else
        {
            await timeBindings.DeleteAsync(new { binding.DimensionBindingId }, cancellationToken);
        }

        return new BindingDetail { Binding = binding, Role = role, TimeBinding = extension };
    }

    /// <summary>A binding row holding the input's attributes and the dimension of its role, or of a time binding its own.</summary>
    private async Task<(DimensionBindingRecord Binding, DimensionRoleRecord? Role)> ToRecordAsync(
        long dimensionBindingId, long versionId, BindingInput input, CancellationToken cancellationToken)
    {
        var role = input.DimensionRoleId is { } dimensionRoleId ? await roles.GetAsync(new { DimensionRoleId = dimensionRoleId }, cancellationToken) : null;
        var binding = new DimensionBindingRecord
        {
            DimensionBindingId = dimensionBindingId,
            DatasetVersionId = versionId,
            DimensionRoleId = input.DimensionRoleId,
            DimensionId = role?.DimensionId ?? input.DimensionId!.Value,
            BindingKind = input.BindingKind,
            SourceFieldId = input.SourceFieldId,
            MappingSetId = input.MappingSetId,
            SourceDimensionBindingId = input.SourceDimensionBindingId,
            BridgeId = input.BridgeId,
            RaggedBehavior = input.RaggedBehavior,
            DefaultPeriodAlignment = input.DefaultPeriodAlignment,
            UnmappedBehavior = input.UnmappedBehavior,
            UnknownMemberKey = input.UnknownMemberKey,
            IsRequired = input.IsRequired,
            IsGroupable = input.IsGroupable,
            IsFilterable = input.IsFilterable,
            IsEnabled = input.IsEnabled
        };
        return (binding, role);
    }

    /// <summary>A time extension row holding the input's attributes; the allowed periodicities become a JSON array.</summary>
    private static TimeBindingRecord ToRecord(long dimensionBindingId, TimeBindingInput input)
    {
        return new TimeBindingRecord
        {
            DimensionBindingId = dimensionBindingId,
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
