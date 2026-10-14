using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Accepts a mapping entry route only when the entry belongs to the mapping version the route names.</summary>
public sealed class MappingEntryReferenceValidator : AbstractValidator<MappingEntryReference>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="entries">Reads the entry.</param>
    public MappingEntryReferenceValidator(ITableGateway<DimensionMappingRecord> entries)
    {
        RuleFor(reference => reference.EntryId)
            .MustAsync(async (reference, entryId, cancellationToken) =>
                (await entries.ListAsync(new { MappingEntryId = entryId, MappingVersionId = reference.VersionId }, cancellationToken)).Count == 1)
            .WithMessage(reference => $"Entry {reference.EntryId} is not an entry of mapping version {reference.VersionId}.");
    }
}
