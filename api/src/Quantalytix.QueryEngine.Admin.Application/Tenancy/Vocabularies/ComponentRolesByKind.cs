using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// The <c>componentRolesByKind</c> relationship: the component roles each measure kind reads and which of those it requires, as
/// <see cref="MeasureComponentRoles"/> checks them. Served by the tenant module until the datasets module declares it.
/// </summary>
public sealed class ComponentRolesByKind : IVocabularyRelation
{
    /// <inheritdoc />
    public string Key => "componentRolesByKind";

    /// <inheritdoc />
    public object Value { get; } = MeasureKinds.Definition.Values.ToDictionary(
        kind => kind.Value,
        kind => (IReadOnlyList<VocabularyComponentRoleRequirement>)[.. Requirements(kind.Value)],
        StringComparer.Ordinal);

    /// <summary>
    /// The roles a kind reads, in the vocabulary's order. <see cref="MeasureComponentRoles.Problems"/> counts each missing required
    /// role and each present role the kind does not read, so a lone role changes the count of an empty measure by -1 when the kind
    /// requires it, 0 when it reads it optionally and +1 when it does not read it.
    /// </summary>
    private static IEnumerable<VocabularyComponentRoleRequirement> Requirements(string measureKind)
    {
        var empty = MeasureComponentRoles.Problems(measureKind, null, []).Count;
        foreach (var role in ComponentRoles.Definition.Values.Select(value => value.Value))
        {
            var change = MeasureComponentRoles.Problems(measureKind, null, [role]).Count - empty;
            if (change <= 0) yield return new VocabularyComponentRoleRequirement { Role = role, Required = change < 0 };
        }
    }
}
