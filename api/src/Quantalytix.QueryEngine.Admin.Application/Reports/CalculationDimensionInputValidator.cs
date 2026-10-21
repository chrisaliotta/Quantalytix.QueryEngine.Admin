using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a calculation dimension: a key free among the report's calculation dimensions, a role that exists, a grouping of its
/// template's members, and a scenario dimension that runs neither break out nor filter by, because a run reads one of its versions
/// at a time. Whether the grouping keys name a level or member of the template's published version is the
/// <c>calculation_grouping_resolve</c> rule's concern, because publishing another dimension version can change the answer.
/// </summary>
public sealed class CalculationDimensionInputValidator : AbstractValidator<CalculationDimensionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Names the route's report and edited dimension.</param>
    /// <param name="calculationDimensions">Reads the report's calculation dimension keys.</param>
    /// <param name="roles">Reads the role.</param>
    public CalculationDimensionInputValidator(
        ContentKeys keys,
        ITableGateway<CalculationDimensionRecord> calculationDimensions,
        ITableGateway<DimensionRoleRecord> roles)
    {
        RuleFor(input => input.DimensionKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync((key, cancellationToken) => keys.KeyFreeAsync(calculationDimensions, new { keys.ReportId, DimensionKey = key },
                dimension => dimension.CalculationDimensionId, cancellationToken))
            .WithMessage(input => $"The report already has a calculation dimension with key '{input.DimensionKey}'.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.RoleId)
            .MustAsync(async (roleId, cancellationToken) => (await roles.ListAsync(new { RoleId = roleId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Role {input.RoleId} does not exist.");
        Include(new GroupingInputValidator<CalculationDimensionInput>(128));
        When(input => input.IsScenario, () =>
        {
            RuleFor(input => input.IsGroupable).Equal(false).WithMessage("A scenario dimension is not groupable: a run reads one of its versions at a time.");
            RuleFor(input => input.IsFilterable).Equal(false).WithMessage("A scenario dimension is not filterable: a run names its version as its scenario.");
        });
    }
}
