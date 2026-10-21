using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>Accepts a source route only when the source belongs to the report the route names.</summary>
public sealed class ReportSourceReferenceValidator : AbstractValidator<ReportSourceReference>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="sources">Reads the source.</param>
    public ReportSourceReferenceValidator(ITableGateway<ReportSourceRecord> sources)
    {
        RuleFor(reference => reference.SourceId)
            .MustAsync(async (reference, sourceId, cancellationToken) =>
                (await sources.ListAsync(new { ReportSourceId = sourceId, reference.ReportId }, cancellationToken)).Count == 1)
            .WithMessage(reference => $"Source {reference.SourceId} is not part of report {reference.ReportId}.");
    }
}
