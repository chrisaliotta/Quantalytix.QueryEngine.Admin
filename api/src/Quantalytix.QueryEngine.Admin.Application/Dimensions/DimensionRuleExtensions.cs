using System.Text.Json;
using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>FluentValidation rules shared by the dimension inputs: stable keys, UTC times and raw JSON text.</summary>
public static class DimensionRuleExtensions
{
    /// <summary>The pattern of a new stable key: a letter, then letters, digits or underscores.</summary>
    public const string KeyPattern = "^[A-Za-z][A-Za-z0-9_]*$";

    /// <summary>
    /// Requires a new stable key: not empty, at most <paramref name="maxLength"/> characters, and matching
    /// <see cref="KeyPattern"/>. Put <c>Cascade(CascadeMode.Stop)</c> in front so an empty key reports one failure.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a key property of the input.</param>
    /// <param name="maxLength">The key column's length.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string> MustBeKey<T>(this IRuleBuilder<T, string> rule, int maxLength)
    {
        return rule
            .NotEmpty()
            .MaximumLength(maxLength)
            .Matches(KeyPattern)
            .WithMessage("'{PropertyName}' must start with a letter and hold only letters, digits and underscores.");
    }

    /// <summary>
    /// Rejects a time that carried a non-UTC offset. JSON times ending in <c>Z</c> or carrying no offset are stored as UTC, as
    /// the query engine reads them; null passes.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a time property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, DateTime?> MustBeUtc<T>(this IRuleBuilder<T, DateTime?> rule)
    {
        return rule
            .Must(time => time is null || time.Value.Kind != DateTimeKind.Local)
            .WithMessage("'{PropertyName}' must be a UTC time, ending in 'Z'.");
    }

    /// <summary>Requires text to parse as JSON; null passes. A failure carries the parser's message, which names the position.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a JSON text property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptionsConditions<T, string?> MustBeJson<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule.Custom((text, context) =>
        {
            if (text is null) return;
            try
            {
                using var document = JsonDocument.Parse(text);
            }
            catch (JsonException exception)
            {
                context.AddFailure($"'{context.DisplayName}' is not valid JSON: {exception.Message}");
            }
        });
    }
}
