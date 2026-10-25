using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>FluentValidation rules for inputs that name a table or view of the tenant database.</summary>
public static class SourceObjectRuleExtensions
{
    /// <summary>
    /// Requires the name to be a table or view that datasets can query, of the kind the input names. Put it behind the kind's
    /// own vocabulary rule, so an unknown kind reports once.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for the source object name.</param>
    /// <param name="kind">Reads the input's <c>source_object_kind</c> value.</param>
    /// <param name="sourceObjects">The tenant's source objects.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeSourceObject<T>(
        this IRuleBuilder<T, string?> rule,
        Func<T, string?> kind,
        SourceObjectService sourceObjects)
    {
        return rule
            .MustAsync((input, name, cancellationToken) => sourceObjects.ExistsAsync(kind(input)!, name!, cancellationToken))
            .WithMessage((input, name) => $"'{name}' is not a {kind(input)} of the tenant database.");
    }
}
