using System.Globalization;
using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>FluentValidation rules for the ids change-set operations carry, which <see cref="TemporaryIds.Resolve"/> reads.</summary>
public static class ChangeSetIdRuleExtensions
{
    /// <summary>Requires a stored id (decimal digits of a positive BIGINT) or a temporary id (<c>tmp:</c> and a name).</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for an id property of an operation.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeChangeSetId<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule
            .Must(id => id is null || IsChangeSetId(id))
            .WithMessage($"'{{PropertyName}}' must be a stored id or a temporary id '{TemporaryIds.Prefix}<uuid>'.");
    }

    /// <summary>Whether text is a stored or temporary id.</summary>
    private static bool IsChangeSetId(string id)
    {
        if (TemporaryIds.IsTemporary(id)) return id.Length > TemporaryIds.Prefix.Length;
        return long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var stored) && stored > 0;
    }
}
