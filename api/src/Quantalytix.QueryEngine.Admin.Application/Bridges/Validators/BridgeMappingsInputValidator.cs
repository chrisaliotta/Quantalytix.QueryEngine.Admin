using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Checks a complete mapping list of an analytical bridge: member keys fit their columns, factors and weights fit the
/// <c>DECIMAL(38, 12)</c> columns, weights are 0 or more, each mapping maps its source member alone, and each source and target
/// pair appears once, because the pair is unique within a version. Whether the endpoint dimensions' published versions have the
/// members and the weights suit the allocation mode are publication rules, because they depend on the version. The cardinality
/// and allocation rules count each source member's targets, so a mapping of a member's descendants, which a report's editor
/// writes into its sources' bridges, is refused here.
/// </summary>
public sealed class BridgeMappingsInputValidator : AbstractValidator<BridgeMappingsInput>
{
    /// <summary>Declares the rules.</summary>
    public BridgeMappingsInputValidator()
    {
        RuleForEach(input => input.Mappings).ChildRules(mapping =>
        {
            mapping.RuleFor(input => input.SourceMemberKey).MustBeMemberKey();
            mapping.RuleFor(input => input.TargetMemberKey).MustBeMemberKey();
            mapping.RuleFor(input => input.Factor).PrecisionScale(38, 12, true);
            mapping.RuleFor(input => input.AllocationWeight).GreaterThanOrEqualTo(0m).PrecisionScale(38, 12, true);
            mapping.RuleFor(input => input.SourceSelection)
                .Equal(SourceSelections.Member)
                .WithMessage("An analytical bridge maps each source member alone; only a report source's bridge maps a member's descendants.");
        });
        RuleFor(input => input.Mappings).Custom((mappings, context) =>
        {
            var repeated = mappings
                .GroupBy(mapping => (mapping.SourceMemberKey, mapping.TargetMemberKey))
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            foreach (var (sourceMemberKey, targetMemberKey) in repeated)
                context.AddFailure($"Source member '{sourceMemberKey}' is mapped to target member '{targetMemberKey}' more than once.");
        });
    }
}
