using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>FluentValidation rules of the mapping set and bridge inputs, which the report editor's rule lists share.</summary>
public static class MappingRuleExtensions
{
    /// <summary>The length of <c>qe_mapping_set.mapping_set_key</c> and <c>qe_bridge.bridge_key</c>.</summary>
    public const int KeyLength = 128;

    /// <summary>Requires an actual source value: not empty and within the 512-byte column. The value is kept exactly as given.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a source value property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeSourceKeyValue<T>(this IRuleBuilder<T, string> rule)
    {
        return rule.NotEmpty().MustFitVarchar(512);
    }

    /// <summary>
    /// Requires a member key: not empty and within the 256-byte column. Whether a member holds it is for the rules, which resolve
    /// it in the dimension's live version.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a member key property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeMemberKey<T>(this IRuleBuilder<T, string> rule)
    {
        return rule.NotEmpty().MustFitVarchar(256);
    }

    /// <summary>Requires a factor or weight that fits the <c>DECIMAL(38, 12)</c> columns.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for the number.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, decimal> MustFitDecimal<T>(this IRuleBuilder<T, decimal> rule)
    {
        return rule.PrecisionScale(38, 12, true);
    }
}
