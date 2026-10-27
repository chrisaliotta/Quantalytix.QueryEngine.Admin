using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Tenancy;

/// <summary>
/// Confirms that <c>componentRolesByKind</c> in <c>GET /api/vocabularies</c> tells the measure editor the same roles the measure
/// rules check, so the editor never offers a component the API refuses or omits one it requires.
/// </summary>
public sealed class ComponentRolesByKindTests
{
    /// <summary>Every measure kind is a key, with the roles it reads and which of those it requires.</summary>
    [Fact]
    public void ListsTheRolesEachKindReads()
    {
        var byKind = (IReadOnlyDictionary<string, IReadOnlyList<VocabularyComponentRoleRequirement>>)new ComponentRolesByKind().Value;

        Assert.Equal(MeasureKinds.Definition.Values.Select(value => value.Value), byKind.Keys);
        Assert.Equal([(ComponentRoles.Value, true)], Roles(byKind[MeasureKinds.Aggregate]));
        Assert.Equal([(ComponentRoles.Value, true), (ComponentRoles.Weight, true)], Roles(byKind[MeasureKinds.WeightedAverage]));
        Assert.Equal([(ComponentRoles.Value, true), (ComponentRoles.PeriodDays, false)], Roles(byKind[MeasureKinds.PeriodAverage]));
        Assert.Empty(byKind[MeasureKinds.Formula]);
        Assert.All(byKind[MeasureKinds.Specialized], requirement => Assert.False(requirement.Required));
        Assert.Equal(ComponentRoles.Definition.Values.Count, byKind[MeasureKinds.Specialized].Count);
    }

    /// <summary>The requirements as role and required pairs.</summary>
    private static IEnumerable<(string, bool)> Roles(IReadOnlyList<VocabularyComponentRoleRequirement> requirements)
    {
        return requirements.Select(requirement => (requirement.Role, requirement.Required));
    }
}
