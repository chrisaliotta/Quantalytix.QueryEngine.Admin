using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// The component roles each measure kind reads (schema section 13): an aggregate reads a value, except that a count without
/// one counts the rows its row filter admits; weighted measures read a value and a weight; a period average reads a value
/// and may verify the source's period length; a formula reads other measures and no fields; a specialized implementation
/// may read any role. The measure input validator and the <c>component_roles_match_kind</c> publication rule share this
/// table.
/// </summary>
public static class MeasureComponentRoles
{
    /// <summary>Every role in vocabulary order.</summary>
    private static readonly List<string> RoleOrder = [.. ComponentRoles.Definition.Values.Select(value => value.Value)];

    /// <summary>A role's position in the vocabulary, the order a measure lists its components in.</summary>
    /// <param name="role">A <c>component_role</c> value.</param>
    /// <returns>The position.</returns>
    public static int Order(string role)
    {
        return RoleOrder.IndexOf(role);
    }

    /// <summary>Describes how a measure's component roles miss or exceed what its kind reads.</summary>
    /// <param name="measureKind">A <c>measure_kind</c> value.</param>
    /// <param name="aggregationFunction">The measure's <c>aggregation_function</c>, or null when it has none.</param>
    /// <param name="roles">The roles of the measure's components.</param>
    /// <returns>
    /// One sentence per missing or unexpected role, such as <c>'weighted_average' measures need a 'weight' component.</c>;
    /// empty when the roles fit the kind.
    /// </returns>
    public static IReadOnlyList<string> Problems(string measureKind, string? aggregationFunction, IEnumerable<string> roles)
    {
        var present = roles.Distinct(StringComparer.Ordinal).ToList();
        var allowed = Allowed(measureKind);
        return
        [
            .. Required(measureKind, aggregationFunction)
                .Where(role => !present.Contains(role))
                .Select(role => $"'{measureKind}' measures need a '{role}' component."),
            .. present
                .Where(role => !allowed.Contains(role))
                .Select(role => $"'{measureKind}' measures take no '{role}' component.")
        ];
    }

    /// <summary>The roles a measure of the kind and aggregation function must have.</summary>
    private static string[] Required(string measureKind, string? aggregationFunction)
    {
        return measureKind switch
        {
            MeasureKinds.Aggregate when aggregationFunction == AggregationFunctions.Count => [],
            MeasureKinds.Aggregate or MeasureKinds.PeriodAverage => [ComponentRoles.Value],
            MeasureKinds.WeightedSum or MeasureKinds.WeightedAverage => [ComponentRoles.Value, ComponentRoles.Weight],
            _ => []
        };
    }

    /// <summary>The roles a measure of the kind may have.</summary>
    private static string[] Allowed(string measureKind)
    {
        return measureKind switch
        {
            MeasureKinds.Aggregate => [ComponentRoles.Value],
            MeasureKinds.WeightedSum or MeasureKinds.WeightedAverage => [ComponentRoles.Value, ComponentRoles.Weight],
            MeasureKinds.PeriodAverage => [ComponentRoles.Value, ComponentRoles.PeriodDays],
            MeasureKinds.Specialized => [.. ComponentRoles.Definition.Values.Select(value => value.Value)],
            _ => []
        };
    }
}
