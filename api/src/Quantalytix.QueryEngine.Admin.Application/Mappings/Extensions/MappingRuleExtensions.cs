using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>FluentValidation rules of the mapping entry inputs, which the bridge mapping list shares.</summary>
public static class MappingRuleExtensions
{
    /// <summary>Requires an actual source value: not empty and within the 512-byte column. The value is kept exactly as given.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a source value property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeSourceKeyValue<T>(this IRuleBuilder<T, string> rule)
    {
        return rule.NotEmpty().MustFitVarchar(512);
    }

    /// <summary>
    /// Requires a member key: not empty and within the 256-byte column. Whether a member holds it is for the publication rules,
    /// which resolve it in the dimension's published version.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a member key property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeMemberKey<T>(this IRuleBuilder<T, string> rule)
    {
        return rule.NotEmpty().MustFitVarchar(256);
    }
}
