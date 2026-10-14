using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Checks a complete mapping list: factors and weights fit the <c>DECIMAL(38, 12)</c> columns, weights are 0 or more, and
/// each source and target pair appears once, because the pair is the table's key and a repeat would silently replace the
/// first. Whether the members belong to the endpoint versions and the weights suit the allocation mode are publication
/// rules, because they depend on the version.
/// </summary>
public sealed class BridgeMappingsInputValidator : AbstractValidator<BridgeMappingsInput>
{
    /// <summary>Declares the rules.</summary>
    public BridgeMappingsInputValidator()
    {
        RuleForEach(input => input.Mappings).ChildRules(mapping =>
        {
            mapping.RuleFor(input => input.Factor).PrecisionScale(38, 12, true);
            mapping.RuleFor(input => input.AllocationWeight).GreaterThanOrEqualTo(0m).PrecisionScale(38, 12, true);
        });
        RuleFor(input => input.Mappings).Custom((mappings, context) =>
        {
            var repeated = mappings
                .GroupBy(mapping => (mapping.SourceMemberId, mapping.TargetMemberId))
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            foreach (var (sourceMemberId, targetMemberId) in repeated)
                context.AddFailure($"Source member {sourceMemberId} is mapped to target member {targetMemberId} more than once.");
        });
    }
}
