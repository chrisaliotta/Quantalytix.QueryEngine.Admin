using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a new dimension role: a well-formed key that no other role of the tenant has, because requests name a role's binding
/// by its key, and the attributes <see cref="DimensionRoleInputValidator"/> checks. A role of an existing dimension also needs
/// an analytical dimension, because a report dimension has no roles.
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
    /// Declares the rules for a new role of the route's dimension, which must be analytical: no dataset binds a report dimension
    /// and no calculation dimension reads it.
    /// </summary>
    /// <param name="roles">Reads existing role keys.</param>
    /// <param name="dimensions">Reads the route's dimension.</param>
    /// <param name="route">Names the dimension the role belongs to.</param>
    public NewDimensionRoleInputValidator(ITableGateway<DimensionRoleRecord> roles, ITableGateway<DimensionRecord> dimensions, IRequestRoute route)
        : this(roles)
    {
        RuleFor(input => input.RoleKey)
            .MustAsync(async (_, cancellationToken) =>
                (await dimensions.GetAsync(new { DimensionId = route.Id("dimensionId") }, cancellationToken)).DimensionPurpose == DimensionPurposes.Analytical)
            .WithMessage("The dimension holds a report's rows, and a report dimension has no roles.");
    }
}
