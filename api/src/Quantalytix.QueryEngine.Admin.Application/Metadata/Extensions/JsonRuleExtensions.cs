using System.Text.Json;
using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>FluentValidation rules for the raw JSON text the JSON columns of the metadata tables hold.</summary>
public static class JsonRuleExtensions
{
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
