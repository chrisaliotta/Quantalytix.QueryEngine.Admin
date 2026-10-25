using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires the roles a dataset version binds to name one thing each: a version binds each role at most once, and a bound role's
/// key is the key of no field of the version other than the one its binding reads. The query engine keys every binding of a
/// version by its role's key and reads the binding's field under that key beside the version's own fields, enabled or not, so
/// a second binding of a role or a field of the same key fails the whole version's metadata load.
/// </summary>
public sealed class BoundRolesDistinctRule(
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<FieldRecord> fields) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "bound_roles_distinct";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each role the version binds more than once, and each bound role whose key another field of the version has.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per repeated role and per role key a field shares.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionId };
        var bound = (await bindings.ListAsync(versionFilter, cancellationToken)).Where(binding => binding.DimensionRoleId is not null).ToList();
        var roleKeys = (await roles.ListAsync(new { DimensionRoleId = bound.Select(binding => binding.DimensionRoleId!.Value).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(role => role.DimensionRoleId, role => role.DimensionRoleKey);
        var fieldIds = (await fields.ListAsync(versionFilter, cancellationToken)).ToLookup(field => field.FieldKey, field => field.FieldId, StringComparer.Ordinal);

        var violations = new List<RuleViolation>();
        foreach (var role in bound.GroupBy(binding => roleKeys[binding.DimensionRoleId!.Value]).OrderBy(role => role.Key, StringComparer.Ordinal))
        {
            if (role.Count() > 1)
                violations.Add(Violation($"Role '{role.Key}' is bound {role.Count()} times; a version binds each role at most once, so its key names one binding."));
            if (role.Any(binding => fieldIds[role.Key].Any(fieldId => fieldId != binding.SourceFieldId)))
                violations.Add(Violation($"Role '{role.Key}' has the key of a field its binding does not read; requests name the binding by the role's key, so no other field may have it."));
        }

        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
