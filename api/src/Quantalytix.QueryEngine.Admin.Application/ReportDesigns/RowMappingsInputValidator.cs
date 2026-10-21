using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Checks one row's complete item list: the row is a data row of the design, each item names a source of the version and an
/// assignable member of the published version of that source's bound dimension, which its binding classifies rows into, each
/// factor fits the <c>DECIMAL(38, 12)</c> column, and each source's item appears once.
/// </summary>
public sealed class RowMappingsInputValidator : AbstractValidator<RowMappingsInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="designs">Reads the route's design scope.</param>
    /// <param name="members">Reads the design's member rows.</param>
    /// <param name="sources">Reads the version's variable sources.</param>
    /// <param name="publishedDimensions">Reads the published versions of the sources' bound dimensions.</param>
    /// <param name="keys">Names the route's report version.</param>
    public RowMappingsInputValidator(
        ReportDesignVersions designs,
        ITableGateway<DimensionMemberRecord> members,
        ITableGateway<VariableSourceRecord> sources,
        PublishedDimensionReader publishedDimensions,
        VersionKeys keys)
    {
        RuleFor(input => input.RowKey)
            .NotEmpty()
            .MustAsync(async (rowKey, cancellationToken) =>
            {
                var scope = await designs.ReadAsync(keys.VersionId, cancellationToken);
                var tree = await members.ListAsync(new { scope.Structure.DimensionVersionId }, cancellationToken);
                return tree.SingleOrDefault(member => member.MemberKey == rowKey) is { IsAssignable: true };
            })
            .WithMessage(input => $"Row '{input.RowKey}' is no data row of the design; only data rows take source items.");
        RuleForEach(input => input.Mappings).ChildRules(mapping =>
        {
            mapping.RuleFor(input => input.ItemKey).MustBeMemberKey();
            mapping.RuleFor(input => input.Factor).PrecisionScale(38, 12, true);
        });
        RuleFor(input => input.Mappings).Custom((mappings, context) =>
        {
            foreach (var repeated in mappings.GroupBy(mapping => (mapping.BridgeId, mapping.ItemKey)).Where(group => group.Count() > 1))
                context.AddFailure($"Item '{repeated.Key.ItemKey}' of source {repeated.Key.BridgeId} appears more than once.");
        });
        RuleFor(input => input.Mappings).CustomAsync(async (mappings, context, cancellationToken) =>
        {
            var scope = await designs.ReadAsync(keys.VersionId, cancellationToken);
            var used = (await sources.ListAsync(new { ReportVersionId = keys.VersionId, ClassificationKind = ClassificationKinds.Bridge }, cancellationToken))
                .Select(source => source.BridgeId!.Value)
                .ToHashSet();
            foreach (var source in mappings.GroupBy(mapping => mapping.BridgeId))
            {
                if (!used.Contains(source.Key))
                {
                    context.AddFailure($"Bridge {source.Key} is no source of this report version.");
                    continue;
                }

                var items = await publishedDimensions.ReadAsync(scope.Bridges[source.Key].SourceDimensionId, cancellationToken);
                foreach (var mapping in source.Where(mapping => items is null || !items.MemberKeys.Contains(mapping.ItemKey) || items.UnassignableMemberKeys.Contains(mapping.ItemKey)))
                    context.AddFailure($"Item '{mapping.ItemKey}' is no assignable member of the published version of the dimension source {source.Key} maps from.");
            }
        });
    }
}
