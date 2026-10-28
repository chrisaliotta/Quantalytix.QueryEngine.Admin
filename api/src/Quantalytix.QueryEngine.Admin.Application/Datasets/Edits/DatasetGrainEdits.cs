using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The grain operations of the dataset change set: keys fixed and unique per version (D4), components fields of the version in
/// ordinal order. A grain a time policy selects by is not deleted.
/// </summary>
internal static class DatasetGrainEdits
{
    /// <summary><c>saveGrain</c>: replaces the grain's components.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the grain saved.</returns>
    public static DatasetSnapshot Save(DatasetSnapshot snapshot, DatasetSaveGrainOperation operation, ChangeContext context)
    {
        var input = operation.Input;
        var grainId = DatasetEditTargets.SavedId(input.Id, context);
        var existing = snapshot.Grains.SingleOrDefault(grain => grain.DatasetGrainId == grainId);
        if (DatasetEditTargets.IsGone(input.Id, existing is not null, "Grain", context)) return snapshot;
        var others = snapshot.Grains.Where(grain => grain.DatasetGrainId != grainId).Select(grain => grain.GrainKey);
        if (DatasetEditTargets.IsKeyRefused(existing?.GrainKey, input.Key, others, "grain", context)) return snapshot;
        var fieldIds = input.FieldIds.Select(context.Ids.Resolve).ToList();
        if (fieldIds.Any(fieldId => snapshot.Fields.All(field => field.FieldId != fieldId)))
        {
            context.Reject("input.fieldIds", "Choose fields of this version.");
            return snapshot;
        }

        var saved = new DatasetGrainRecord { DatasetGrainId = grainId, DatasetVersionId = snapshot.VersionId, GrainKey = input.Key, GrainKind = input.GrainKind };
        return snapshot with
        {
            Grains = [.. snapshot.Grains.Where(grain => grain.DatasetGrainId != grainId), saved],
            GrainFields =
            [
                .. snapshot.GrainFields.Where(component => component.DatasetGrainId != grainId),
                .. fieldIds.Select((fieldId, index) => new DatasetGrainFieldRecord { DatasetGrainId = grainId, Ordinal = index + 1, FieldId = fieldId })
            ]
        };
    }

    /// <summary><c>deleteGrain</c>: refused while a time policy selects by the grain.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate without the grain and its components.</returns>
    public static DatasetSnapshot Delete(DatasetSnapshot snapshot, DatasetDeleteGrainOperation operation, ChangeContext context)
    {
        if (DatasetEditTargets.Find(snapshot.Grains, grain => grain.DatasetGrainId, operation.GrainId, "Grain", context) is not { } grain) return snapshot;
        var check = DatasetDeleteRules.Item(snapshot, grain.DatasetGrainId, $"Grain '{grain.GrainKey}'", DatasetItemBlockerCodes.GrainInUse);
        if (!check.Allowed)
        {
            context.Blocked(check);
            return snapshot;
        }

        return snapshot with
        {
            Grains = [.. snapshot.Grains.Where(row => row.DatasetGrainId != grain.DatasetGrainId)],
            GrainFields = [.. snapshot.GrainFields.Where(component => component.DatasetGrainId != grain.DatasetGrainId)]
        };
    }
}
