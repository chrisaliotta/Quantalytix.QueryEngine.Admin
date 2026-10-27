using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// <c>bound_roles_distinct</c> (D6): a version binds each role at most once, and a bound role's key names no field of the version
/// other than the binding's own source field, because requests name the binding by the role's key.
/// </summary>
public sealed class BoundRolesDistinctRule : BindingRule
{
    /// <summary><c>bound_roles_distinct</c>.</summary>
    protected override string Code => "bound_roles_distinct";

    /// <summary>Reports every role bound twice and every bound role whose key another field has.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>At most two violations per role.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names)
    {
        var fieldIds = snapshot.Fields.ToLookup(field => field.FieldKey, field => field.FieldId, StringComparer.Ordinal);
        foreach (var role in snapshot.Bindings.Where(binding => binding.DimensionRoleId is not null).GroupBy(binding => names[binding.DimensionBindingId]).OrderBy(role => role.Key, StringComparer.Ordinal))
        {
            var first = role.OrderBy(binding => binding.DimensionBindingId).First();
            if (role.Count() > 1)
                yield return Violation(first, names, $"Role '{role.Key}' is bound {role.Count()} times; a version binds each role at most once, so its key names one binding.");
            if (role.Any(binding => fieldIds[role.Key].Any(fieldId => fieldId != binding.SourceFieldId)))
                yield return Violation(first, names, $"Role '{role.Key}' has the key of a field its binding does not read; requests name the binding by the role's key, so no other field may have it.");
        }
    }
}
