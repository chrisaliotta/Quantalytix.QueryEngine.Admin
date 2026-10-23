using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a new dimension role: a well-formed key that no other role of the tenant has, because requests name a role's binding
/// by its key, and the attributes <see cref="DimensionRoleInputValidator"/> checks. A role of an existing dimension also needs
/// an analytical dimension, because a report dimension has no roles, and a dimension whose role is a scenario of a report takes no
/// other role, because a scenario's dimension has exactly one role, which <see cref="ReportScenariosInputValidator"/> requires of
/// every scenario a report names.
/// </summary>
public sealed class NewDimensionRoleInputValidator : AbstractValidator<NewDimensionRoleInput>
{
    /// <summary>Declares the rules for the first role of a new dimension, which is analytical.</summary>
    /// <param name="roles">Reads existing role keys.</param>
    public NewDimensionRoleInputValidator(ITableGateway<DimensionRoleRecord> roles)
    {
        Include(new DimensionRoleInputValidator());
        RuleFor(input => input.RoleKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await roles.ListAsync(new { RoleKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"A role with key '{input.RoleKey}' already exists.");
    }

    /// <summary>
    /// Declares the rules for a new role of the route's dimension, which must be analytical, since no dataset binds a report
    /// dimension and no calculation dimension reads it, and whose role no report names as a scenario.
    /// </summary>
    /// <param name="roles">Reads existing role keys and the dimension's roles.</param>
    /// <param name="dimensions">Reads the route's dimension.</param>
    /// <param name="scenarios">Reads the report scenarios that name the dimension's role.</param>
    /// <param name="reportNames">Names the reports of those scenarios.</param>
    /// <param name="route">Names the dimension the role belongs to.</param>
    public NewDimensionRoleInputValidator(
        ITableGateway<DimensionRoleRecord> roles,
        ITableGateway<DimensionRecord> dimensions,
        ITableGateway<ReportScenarioRecord> scenarios,
        ReportNames reportNames,
        IRequestRoute route)
        : this(roles)
    {
        RuleFor(input => input.RoleKey)
            .MustAsync(async (_, cancellationToken) =>
                (await dimensions.GetAsync(new { DimensionId = route.Id("dimensionId") }, cancellationToken)).DimensionPurpose == DimensionPurposes.Analytical)
            .WithMessage("The dimension holds a report's rows, and a report dimension has no roles.");
        RuleFor(input => input.RoleKey).CustomAsync(async (_, context, cancellationToken) =>
        {
            var existing = await roles.ListAsync(new { DimensionId = route.Id("dimensionId") }, cancellationToken);
            var naming = await scenarios.ListAsync(new { RoleId = existing.Select(role => role.RoleId).ToArray() }, cancellationToken);
            if (naming.Count == 0) return;
            var names = await reportNames.OfIdsAsync(naming.Select(scenario => scenario.ReportId), cancellationToken);
            context.AddFailure(
                $"Role '{existing.Single().RoleKey}' of the dimension is a scenario of {string.Join(", ", names.Values.Order(StringComparer.Ordinal))}, " +
                "and a scenario's dimension has exactly one role.");
        });
    }
}
