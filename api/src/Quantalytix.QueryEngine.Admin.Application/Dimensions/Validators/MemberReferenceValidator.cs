using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Accepts a member route only when the member belongs to the dimension version the route names.</summary>
public sealed class MemberReferenceValidator : AbstractValidator<MemberReference>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="members">Reads the member.</param>
    public MemberReferenceValidator(ITableGateway<DimensionMemberRecord> members)
    {
        RuleFor(reference => reference.DimensionMemberId)
            .MustAsync(async (reference, dimensionMemberId, cancellationToken) =>
                (await members.ListAsync(new { DimensionMemberId = dimensionMemberId, reference.DimensionVersionId }, cancellationToken)).Count == 1)
            .WithMessage(reference => $"Member {reference.DimensionMemberId} is not a member of dimension version {reference.DimensionVersionId}.");
    }
}
