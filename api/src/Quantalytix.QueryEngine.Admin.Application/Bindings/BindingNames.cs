using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Names dimension bindings, which have no key or name of their own, the way requests name them: a role binding by its role's
/// key and a time binding by its field's key.
/// </summary>
public sealed class BindingNames(ITableGateway<DimensionRoleRecord> roles, ITableGateway<FieldRecord> fields)
{
    /// <summary>Names each binding.</summary>
    /// <param name="bindings">The bindings to name.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Each binding's name by binding ID.</returns>
    public async Task<IReadOnlyDictionary<long, string>> OfAsync(IReadOnlyCollection<DimensionBindingRecord> bindings, CancellationToken cancellationToken)
    {
        long[] roleIds = [.. bindings.Select(binding => binding.RoleId).OfType<long>().Distinct()];
        long[] fieldIds = [.. bindings.Where(binding => binding.RoleId is null).Select(binding => binding.SourceFieldId).OfType<long>().Distinct()];
        var roleKeys = (await roles.ListAsync(new { RoleId = roleIds }, cancellationToken)).ToDictionary(role => role.RoleId, role => role.RoleKey);
        var fieldKeys = (await fields.ListAsync(new { FieldId = fieldIds }, cancellationToken)).ToDictionary(field => field.FieldId, field => field.FieldKey);
        return bindings.ToDictionary(binding => binding.BindingId, binding => binding.RoleId is { } roleId ? roleKeys[roleId] : fieldKeys[binding.SourceFieldId!.Value]);
    }
}
