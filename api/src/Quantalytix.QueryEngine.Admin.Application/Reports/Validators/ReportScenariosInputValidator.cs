using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a complete scenario list: roles that exist, each named once and each the only role of its dimension. Requests name each
/// scenario by its role's key, and a scenario's dimension has exactly one role, so the scenario is its dimension and every dataset
/// carries its members through that one role; the dimensions module's <c>saveRole</c> operation keeps the dimension so.
/// Whether a calculation dimension of the report reads a scenario's role is the <c>scenario_roles_distinct</c> rule's concern,
/// because the calculation dimensions change too.
/// </summary>
public sealed class ReportScenariosInputValidator : AbstractValidator<ReportScenariosInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="roles">Reads the roles and the other roles of their dimensions.</param>
    public ReportScenariosInputValidator(ITableGateway<DimensionRoleRecord> roles)
    {
        RuleForEach(input => input.Scenarios).ChildRules(scenario =>
        {
            scenario.RuleFor(input => input.DimensionRoleId).CustomAsync(async (dimensionRoleId, context, cancellationToken) =>
            {
                if ((await roles.ListAsync(new { DimensionRoleId = dimensionRoleId }, cancellationToken)).SingleOrDefault() is not { } role)
                {
                    context.AddFailure($"Role {dimensionRoleId} does not exist.");
                    return;
                }
                string[] others =
                [
                    .. (await roles.ListAsync(new { role.DimensionId }, cancellationToken))
                        .Where(other => other.DimensionRoleId != dimensionRoleId)
                        .Select(other => $"'{other.DimensionRoleKey}'")
                        .Order(StringComparer.Ordinal)
                ];
                if (others.Length > 0)
                    context.AddFailure($"Role '{role.DimensionRoleKey}' shares its dimension with {string.Join(", ", others)}, and a scenario's dimension has exactly one role.");
            });
        });
        RuleFor(input => input.Scenarios)
            .Must(list => list.DistinctBy(scenario => scenario.DimensionRoleId).Count() == list.Count)
            .WithMessage("'{PropertyName}' must name each role once.");
    }
}
