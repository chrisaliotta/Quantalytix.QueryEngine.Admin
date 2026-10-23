using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a calculation dimension: a key free among the report's calculation dimensions, a role that exists and a grouping of its
/// template's members. Whether the grouping keys name a level or member of the template's published version is the
/// <c>calculation_grouping_resolve</c> rule's concern, because publishing another dimension version can change the answer, and
/// whether the role is one of the report's scenarios is the <c>scenario_roles_distinct</c> rule's, because the scenarios change too.
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
        RuleFor(input => input.DimensionRoleId)
            .MustAsync(async (dimensionRoleId, cancellationToken) => (await roles.ListAsync(new { DimensionRoleId = dimensionRoleId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Role {input.DimensionRoleId} does not exist.");
        Include(new GroupingInputValidator<CalculationDimensionInput>(128));
    }
}
