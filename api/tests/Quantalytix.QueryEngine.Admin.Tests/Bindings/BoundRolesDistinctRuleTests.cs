using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms <c>bound_roles_distinct</c> (D6): a version binds each role at most once, and a bound role's key names no other field,
/// because requests name a binding by its role's key.
/// </summary>
public sealed class BoundRolesDistinctRuleTests
{
    /// <summary>A role bound twice is reported once, naming the role.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RoleBoundTwice_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(
            DatasetRows.Binding(21, 100, 81, BindingKinds.Direct, 1),
            DatasetRows.Binding(22, 100, 81, BindingKinds.Direct, 1) with { IsEnabled = false }));

        Assert.Equal("bound_roles_distinct", violation.Code);
        Assert.Equal("Role 'originationBranch' is bound 2 times; a version binds each role at most once, so its key names one binding.", violation.Message);
    }

    /// <summary>A role whose key is another field's key is reported, because the role's field would shadow that field.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RoleKeyOfAnotherField_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(DatasetRows.Binding(21, 100, 82, BindingKinds.Direct, 1)));

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
            DatasetRows.Binding(21, 100, 81, BindingKinds.Direct, 1),
            DatasetRows.Binding(22, 100, 83, BindingKinds.Direct, 1),
            DatasetRows.Binding(23, 100, null, BindingKinds.Time, 2)));
    }

    /// <summary>
    /// Evaluates the rule on a version with fields branchCode (1) and servicingBranch (2), the roles originationBranch (81),
    /// servicingBranch (82) and branchCode (83), and the given bindings.
    /// </summary>
    private static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params DimensionBindingRecord[] bindings)
    {
        var snapshot = DatasetRows.Snapshot(
            fields: [DatasetRows.ColumnField(1, 100, "branchCode", "branch_code"), DatasetRows.ColumnField(2, 100, "servicingBranch", "servicing_branch")],
            bindings: bindings,
            roles: [DimensionRows.Role(81, "originationBranch", 7000), DimensionRows.Role(82, "servicingBranch", 7000), DimensionRows.Role(83, "branchCode", 7000)]);
        return new BoundRolesDistinctRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);
    }
}
