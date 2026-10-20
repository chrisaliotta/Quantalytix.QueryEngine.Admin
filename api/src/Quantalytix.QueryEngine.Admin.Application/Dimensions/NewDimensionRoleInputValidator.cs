using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a new dimension role: a well-formed key that no other role of the tenant has, because requests name a role's binding
/// by its key, and the attributes <see cref="DimensionRoleInputValidator"/> checks.
/// </summary>
public sealed class NewDimensionRoleInputValidator : AbstractValidator<NewDimensionRoleInput>
{
    /// <summary>Declares the rules.</summary>
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
}
