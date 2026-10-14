using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks one entry: a source value within the 512-byte column, and a member that exists. Whether the member belongs
/// to the pinned dimension version and is assignable are publication rules, because they depend on the version.
/// </summary>
public sealed class MappingEntryInputValidator : AbstractValidator<MappingEntryInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="members">Reads the member.</param>
    public MappingEntryInputValidator(ITableGateway<DimensionMemberRecord> members)
    {
        RuleFor(input => input.SourceKeyValue).MustBeSourceKeyValue();
        RuleFor(input => input.MemberId)
            .MustAsync(async (memberId, cancellationToken) => (await members.ListAsync(new { MemberId = memberId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Member {input.MemberId} does not exist.");
    }
}
