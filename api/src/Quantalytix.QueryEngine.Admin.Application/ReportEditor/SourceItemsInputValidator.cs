using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Checks one row's complete item list for one source: the source is a bridge source, the row is a data row of the report, each
/// item is an assignable member of the published version of the source's item dimension, which its binding classifies rows into,
/// each factor fits the <c>DECIMAL(38, 12)</c> column, and each item appears once. Every key is checked here, and no report rule
/// reads items, so item writes need no check against the report rules.
/// </summary>
public sealed class SourceItemsInputValidator : AbstractValidator<SourceItemsInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="reports">Reads the report.</param>
    /// <param name="sources">Reads the route's source.</param>
    /// <param name="roles">Reads the source's role.</param>
    /// <param name="owned">Reads the report dimension's version.</param>
    /// <param name="members">Reads the report's member rows.</param>
    /// <param name="publishedDimensions">Reads the published version of the source's item dimension.</param>
    /// <param name="keys">Names the route's report and source.</param>
    public SourceItemsInputValidator(
        ITableGateway<ReportRecord> reports,
        ITableGateway<ReportSourceRecord> sources,
        ITableGateway<DimensionRoleRecord> roles,
        OwnedVersions owned,
        ITableGateway<DimensionMemberRecord> members,
        PublishedDimensionReader publishedDimensions,
        ContentKeys keys)
    {
        RuleFor(input => input.RowKey)
            .NotEmpty()
            .MustAsync(async (rowKey, cancellationToken) =>
            {
                var structure = await owned.StructureAsync(await reports.GetAsync(new { keys.ReportId }, cancellationToken), cancellationToken);
                var tree = await members.ListAsync(new { structure.DimensionVersionId }, cancellationToken);
                return tree.SingleOrDefault(member => member.MemberKey == rowKey) is { IsAssignable: true };
            })
            .WithMessage(input => $"Row '{input.RowKey}' is no data row of the report; only data rows take items.");
        RuleForEach(input => input.Items).ChildRules(item =>
        {
            item.RuleFor(input => input.ItemKey).MustBeMemberKey();
            item.RuleFor(input => input.Factor).PrecisionScale(38, 12, true);
        });
        RuleFor(input => input.Items)
            .Must(items => items.DistinctBy(item => item.ItemKey).Count() == items.Count)
            .WithMessage("'{PropertyName}' must list each item once.");
        RuleFor(input => input.Items).CustomAsync(async (items, context, cancellationToken) =>
        {
            var source = await sources.GetAsync(new { ReportSourceId = keys.SourceId }, cancellationToken);
            if (source.RoleId is not { } roleId)
            {
                context.AddFailure($"Source '{source.SourceKey}' is a direct source; its field's values name its rows.");
                return;
            }

            var role = await roles.GetAsync(new { RoleId = roleId }, cancellationToken);
            var itemKeys = await publishedDimensions.ReadAsync(role.DimensionId, cancellationToken);
            foreach (var item in items.Where(item => itemKeys is null || !itemKeys.MemberKeys.Contains(item.ItemKey) || itemKeys.UnassignableMemberKeys.Contains(item.ItemKey)))
                context.AddFailure($"Item '{item.ItemKey}' is no assignable member of the published version of the dimension source '{source.SourceKey}' maps from.");
        });
    }
}
