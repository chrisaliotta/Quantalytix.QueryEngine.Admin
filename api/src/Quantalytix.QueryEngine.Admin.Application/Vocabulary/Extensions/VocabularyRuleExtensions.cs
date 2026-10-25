using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>FluentValidation rules that restrict a string to a controlled vocabulary.</summary>
public static class VocabularyRuleExtensions
{
    /// <summary>
    /// Requires the value to be one of the vocabulary's values. Null passes, as with FluentValidation's built-in rules, so
    /// the same rule serves nullable columns; non-nullable inputs never carry null because the API's JSON input enforces
    /// nullable annotations. (C# cannot overload on nullability alone, so one rule covers both.)
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a string property of the input.</param>
    /// <param name="vocabulary">The vocabulary the value must belong to, such as <see cref="PublicationStatuses.Definition"/>.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeIn<T>(this IRuleBuilder<T, string?> rule, Vocabulary vocabulary)
    {
        string[] allowed = [.. vocabulary.Values.Select(value => value.Value)];
        var message = $"is not a valid {vocabulary.Name}. Allowed values: {string.Join(", ", allowed)}.";
        return rule
            .Must(value => value is null || allowed.Contains(value))
            .WithMessage((_, value) => $"'{value}' {message}");
    }
}
