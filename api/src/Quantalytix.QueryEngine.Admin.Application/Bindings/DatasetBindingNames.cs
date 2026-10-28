using Quantalytix.QueryEngine.Admin.Application.Datasets;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Names the bindings of a dataset version snapshot as <see cref="BindingNames"/> names stored ones: by the bound role's key, which
/// requests name the binding by, or for a time binding by its source field's key.
/// </summary>
public static class DatasetBindingNames
{
    /// <summary>The name of every binding of a snapshot.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>Each binding's name, by binding id.</returns>
    public static IReadOnlyDictionary<long, string> Of(DatasetSnapshot snapshot)
    {
        var roleKeys = snapshot.Surroundings.Roles.ToDictionary(role => role.DimensionRoleId, role => role.DimensionRoleKey);
        var fieldKeys = snapshot.Fields.ToDictionary(field => field.FieldId, field => field.FieldKey);
        return snapshot.Bindings.ToDictionary(
            binding => binding.DimensionBindingId,
            binding => binding.DimensionRoleId is { } roleId ? roleKeys[roleId] : fieldKeys[binding.SourceFieldId!.Value]);
    }
}
