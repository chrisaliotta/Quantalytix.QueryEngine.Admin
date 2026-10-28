using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>FluentValidation rules for the stable keys new metadata rows carry, such as field, level and measure keys.</summary>
public static class KeyRuleExtensions
{
    /// <summary>
    /// The pattern of a new stable key: a letter, then letters, digits or underscores. It must match the whole text, so a
    /// trailing newline fails too.
    /// </summary>
    public const string KeyPattern = @"^[A-Za-z][A-Za-z0-9_]*\z";

    /// <summary>
    /// The contract's <c>DottedKey</c>: a <see cref="KeyPattern"/> that also allows <c>.</c>, as the seeded dataset, model, mapping set
    /// and bridge keys do (OI-29), such as <c>bank.balance_monthly</c>.
    /// </summary>
    public const string DottedKeyPattern = @"^[A-Za-z][A-Za-z0-9_.]*\z";

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
            .WithMessage("'{PropertyName}' must start with a letter and contain only letters, digits and '_'.");
    }

    /// <summary>
    /// Requires a new dotted key: not empty, at most <paramref name="maxLength"/> characters, and matching
    /// <see cref="DottedKeyPattern"/>. Put <c>Cascade(CascadeMode.Stop)</c> in front so an empty key reports one failure.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a key property of the input.</param>
    /// <param name="maxLength">The key column's length.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string> MustBeDottedKey<T>(this IRuleBuilder<T, string> rule, int maxLength)
    {
        return rule
            .NotEmpty()
            .MaximumLength(maxLength)
            .Matches(DottedKeyPattern)
            .WithMessage("'{PropertyName}' must start with a letter and contain only letters, digits, '_' and '.'.");
    }
}
