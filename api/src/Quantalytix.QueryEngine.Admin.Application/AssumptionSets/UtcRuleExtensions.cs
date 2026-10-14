using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>FluentValidation rules for timestamps the API stores as UTC.</summary>
public static class UtcRuleExtensions
{
    /// <summary>
    /// Requires a UTC time. JSON times parse as UTC only with a <c>Z</c> suffix; a time without a zone, or with an offset,
    /// would be stored shifted.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a timestamp property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, DateTime> MustBeUtc<T>(this IRuleBuilder<T, DateTime> rule)
    {
        return rule
            .Must(value => value.Kind == DateTimeKind.Utc)
            .WithMessage("'{PropertyName}' must be a UTC time, such as 2026-09-30T00:00:00Z.");
    }
}
