using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>FluentValidation rules for the times the metadata tables store as UTC, the way the query engine reads them.</summary>
public static class UtcRuleExtensions
{
    /// <summary>The message of a time that is not UTC.</summary>
    private const string Message = "'{PropertyName}' must be a UTC time, such as 2026-09-30T00:00:00Z.";

    /// <summary>
    /// Requires a UTC time. JSON times parse as UTC only with a <c>Z</c> suffix; a time without a zone is ambiguous, and one
    /// with an offset parses as local time, which the StarRocks connection refuses to write.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a time property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, DateTime> MustBeUtc<T>(this IRuleBuilder<T, DateTime> rule)
    {
        return rule.Must(time => time.Kind == DateTimeKind.Utc).WithMessage(Message);
    }

    /// <summary>Requires an optional time to be UTC when it is given, as the non-nullable overload does; null passes.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for an optional time property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, DateTime?> MustBeUtc<T>(this IRuleBuilder<T, DateTime?> rule)
    {
        return rule.Must(time => time is null || time.Value.Kind == DateTimeKind.Utc).WithMessage(Message);
    }
}
