using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// The binding operations of the dataset change set. A non-time binding binds a role of the tenant and classifies into the role's
/// dimension; a time binding names its time dimension and carries its time axis. The server keeps <c>is_required</c>: a required
/// binding is never disabled or deleted. A version binds each role once (D6).
/// </summary>
internal static class DatasetBindingEdits
{
    /// <summary><c>saveBinding</c>: replaces the binding and its time axis.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the binding saved.</returns>
    public static DatasetSnapshot Save(DatasetSnapshot snapshot, DatasetSaveBindingOperation operation, ChangeContext context)
    {
        var input = operation.Input;
        var bindingId = DatasetEditTargets.SavedId(input.Id, context);
        var existing = snapshot.Bindings.SingleOrDefault(binding => binding.DimensionBindingId == bindingId);
        if (DatasetEditTargets.IsGone(input.Id, existing is not null, "Binding", context)) return snapshot;

        var roleId = DatasetEditTargets.Optional(input.DimensionRoleId, context);
        var role = roleId is { } id ? snapshot.Surroundings.Roles.SingleOrDefault(row => row.DimensionRoleId == id) : null;
        var dimensionId = input.BindingKind == BindingKinds.Time ? DatasetEditTargets.Optional(input.DimensionId, context) : role?.DimensionId;
        var sourceFieldId = DatasetEditTargets.Optional(input.SourceFieldId, context);
        var sourceBindingId = DatasetEditTargets.Optional(input.SourceDimensionBindingId, context);
        var isRequired = existing?.IsRequired ?? false;

        if (roleId is not null && role is null) context.Reject("input.dimensionRoleId", "Choose a role of this tenant.");
        else if (roleId is not null && snapshot.Bindings.Any(binding => binding.DimensionBindingId != bindingId && binding.DimensionRoleId == roleId))
            context.Reject("input.dimensionRoleId", "This role is already bound in this version.");
        if (input.BindingKind == BindingKinds.Time && snapshot.Surroundings.Dimensions.All(dimension => dimension.DimensionId != dimensionId))
            context.Reject("input.dimensionId", "Choose a dimension of this tenant.");
        if (sourceFieldId is { } fieldId && snapshot.Fields.All(field => field.FieldId != fieldId)) context.Reject("input.sourceFieldId", "Choose a field of this version.");
        if (sourceBindingId == bindingId) context.Reject("input.sourceDimensionBindingId", "A binding cannot bridge from itself.");
        if (isRequired && !input.IsEnabled) context.Reject("input.isEnabled", "A required binding cannot be disabled.");
        if (context.Refused) return snapshot;

        var saved = new DimensionBindingRecord
        {
            DimensionBindingId = bindingId,
            DatasetVersionId = snapshot.VersionId,
            DimensionRoleId = roleId,
            DimensionId = dimensionId!.Value,
            BindingKind = input.BindingKind,
            SourceFieldId = sourceFieldId,
            MappingSetId = DatasetEditTargets.Optional(input.MappingSetId, context),
            SourceDimensionBindingId = sourceBindingId,
            BridgeId = DatasetEditTargets.Optional(input.BridgeId, context),
            RaggedBehavior = input.RaggedBehavior,
            DefaultPeriodAlignment = input.DefaultPeriodAlignment,
            UnmappedBehavior = input.UnmappedBehavior,
            UnknownMemberKey = input.UnknownMemberKey,
            IsRequired = isRequired,
            IsGroupable = input.IsGroupable,
            IsFilterable = input.IsFilterable,
            IsEnabled = input.IsEnabled
        };
        IReadOnlyList<TimeBindingRecord> time = input.Time is { } axis ? [TimeOf(bindingId, axis, context)] : [];
        return snapshot with
        {
            Bindings = [.. snapshot.Bindings.Where(binding => binding.DimensionBindingId != bindingId), saved],
            TimeBindings = [.. snapshot.TimeBindings.Where(row => row.DimensionBindingId != bindingId), .. time]
        };
    }

    /// <summary><c>deleteBinding</c>: refused for a required binding and while anything names the binding by id.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate without the binding and its time axis.</returns>
    public static DatasetSnapshot Delete(DatasetSnapshot snapshot, DatasetDeleteBindingOperation operation, ChangeContext context)
    {
        if (DatasetEditTargets.Find(snapshot.Bindings, binding => binding.DimensionBindingId, operation.BindingId, "Binding", context) is not { } binding) return snapshot;
        var check = DatasetDeleteRules.Binding(snapshot, binding);
        if (!check.Allowed)
        {
            context.Blocked(check);
            return snapshot;
        }

        return snapshot with
        {
            Bindings = [.. snapshot.Bindings.Where(row => row.DimensionBindingId != binding.DimensionBindingId)],
            TimeBindings = [.. snapshot.TimeBindings.Where(row => row.DimensionBindingId != binding.DimensionBindingId)]
        };
    }

    /// <summary>The time axis row of a time binding.</summary>
    private static TimeBindingRecord TimeOf(long bindingId, DatasetTimeBinding axis, ChangeContext context)
    {
        return new TimeBindingRecord
        {
            DimensionBindingId = bindingId,
            TimeValueType = axis.TimeValueType,
            ObservationKind = axis.ObservationKind,
            SourceTimeZone = axis.SourceTimeZone,
            NativePeriodicity = axis.NativePeriodicity,
            IntervalEndFieldId = DatasetEditTargets.Optional(axis.IntervalEndFieldId, context),
            AllowedPeriodicitiesJson = JsonSerializer.Serialize(axis.AllowedPeriodicities),
            DefaultQueryTimeZone = axis.DefaultQueryTimeZone,
            CumulativeOrigin = axis.CumulativeOrigin,
            CumulativeFixedStart = axis.CumulativeFixedStart?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };
    }
}
