using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms that <c>bound_roles_distinct</c> reports a role a version binds twice and a bound role whose key another field of
/// the version has, and lets one field feed several roles. A request names a binding by its role's key, and the query engine
/// reads the binding's field under that key beside the version's own fields, so either clash makes the key ambiguous and fails
/// the version's metadata load.
/// </summary>
public sealed class BoundRolesDistinctRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The version's fields: branchCode (1) and servicingBranch (2).</summary>
    private readonly InMemoryTableGateway<FieldRecord> _fields = new(
        DatasetRows.ColumnField(1, VersionId, "branchCode", "branch_code"),
        DatasetRows.ColumnField(2, VersionId, "servicingBranch", "servicing_branch"));

    /// <summary>The roles of dimension 7000: originationBranch (81), servicingBranch (82) and branchCode (83).</summary>
    private readonly InMemoryTableGateway<DimensionRoleRecord> _roles = new(
        DimensionRows.Role(81, "originationBranch", 7000),
        DimensionRows.Role(82, "servicingBranch", 7000),
        DimensionRows.Role(83, "branchCode", 7000));

    /// <summary>A role bound twice is reported once, naming the role.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RoleBoundTwice_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(
            DatasetRows.Binding(21, VersionId, 81, BindingKinds.Direct, 1),
            DatasetRows.Binding(22, VersionId, 81, BindingKinds.Direct, 1) with { IsEnabled = false }));

        Assert.Equal(BoundRolesDistinctRule.Code, violation.Code);
        Assert.Equal("Role 'originationBranch' is bound 2 times; a version binds each role at most once, so its key names one binding.", violation.Message);
    }

    /// <summary>A role whose key is another field's key is reported, because the role's field would shadow that field.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RoleKeyOfAnotherField_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(DatasetRows.Binding(21, VersionId, 82, BindingKinds.Direct, 1)));

        Assert.Equal(
            "Role 'servicingBranch' has the key of a field its binding does not read; requests name the binding by the role's key, so no other field may have it.",
            violation.Message);
    }

    /// <summary>One field feeding several roles passes, and so does a role keyed like the field its own binding reads.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task OneFieldFeedingSeveralRoles_Passes()
    {
        Assert.Empty(await EvaluateAsync(
            DatasetRows.Binding(21, VersionId, 81, BindingKinds.Direct, 1),
            DatasetRows.Binding(22, VersionId, 83, BindingKinds.Direct, 1),
            DatasetRows.Binding(23, VersionId, null, BindingKinds.Time, 2)));
    }

    /// <summary>Evaluates the rule on a version holding the given bindings.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params DimensionBindingRecord[] bindings)
    {
        var rule = new BoundRolesDistinctRule(new InMemoryTableGateway<DimensionBindingRecord>(bindings), _roles, _fields);
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }
}
