using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Checks a new report source against the active release, which reports run against: a well-formed key free among the report's
/// sources, a dataset the release pins, and a time field with a time binding. A bridge source names a role the dataset binds into
/// members, which only analytical dimensions have, its bridge's key is free, and the source it copies entries from is a bridge
/// source of the report with the same item dimension. A direct source names an enabled field.
/// </summary>
public sealed class NewReportSourceInputValidator : AbstractValidator<NewReportSourceInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="reports">Reads the report's key.</param>
    /// <param name="sources">Reads the report's sources.</param>
    /// <param name="bridges">Reads the bridge keys.</param>
    /// <param name="roles">Reads the dimensions of the roles a copy compares.</param>
    /// <param name="releaseDatasets">Reads the active release's version of the dataset.</param>
    /// <param name="keys">Names the route's report.</param>
    public NewReportSourceInputValidator(
        ITableGateway<ReportRecord> reports,
        ITableGateway<ReportSourceRecord> sources,
        ITableGateway<BridgeRecord> bridges,
        ITableGateway<DimensionRoleRecord> roles,
        ReleaseDatasets releaseDatasets,
        ContentKeys keys)
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.SourceKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await sources.ListAsync(new { keys.ReportId, SourceKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"The report already has a source with key '{input.SourceKey}'.");
        RuleFor(input => input.MappingMethod).MustBeIn(ReportMappingMethods.Definition);
        RuleFor(input => input.DatasetId)
            .MustAsync(async (datasetId, cancellationToken) => await releaseDatasets.ActiveAsync(datasetId, cancellationToken) is not null)
            .WithMessage(input => $"Dataset {input.DatasetId} is not in the active release, which reports run against.");
        RuleFor(input => input.TimeFieldKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustFitVarchar(128)
            .MustAsync(async (input, key, cancellationToken) => (await releaseDatasets.ActiveAsync(input.DatasetId, cancellationToken))?.IsTimeField(key!) == true)
            .WithMessage(input => $"Field '{input.TimeFieldKey}' has no time binding in the active release's version of the dataset.");
        When(input => input.MappingMethod == ReportMappingMethods.Bridge, () =>
        {
            RuleFor(input => input.FieldKey).Null().WithMessage("A bridge source maps rows through its bridge, so '{PropertyName}' must be empty.");
            RuleFor(input => input.SourceKey)
                .MustAsync(async (key, cancellationToken) =>
                {
                    var bridgeKey = ReportSourceKeys.Bridge((await reports.GetAsync(new { keys.ReportId }, cancellationToken)).ReportKey, key);
                    return bridgeKey.Length <= 128 && (await bridges.ListAsync(new { BridgeKey = bridgeKey }, cancellationToken)).Count == 0;
                })
                .WithMessage(input => $"The source's bridge takes the report's key and '{input.SourceKey}' as its key, which is longer than 128 characters or taken.");
            RuleFor(input => input.DimensionRoleId)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("A bridge source starts from a role the dataset binds, so '{PropertyName}' is required.")
                .MustAsync(async (input, dimensionRoleId, cancellationToken) =>
                    await releaseDatasets.ActiveAsync(input.DatasetId, cancellationToken) is { } dataset && SourceBindings.BridgesFrom(dataset, dimensionRoleId!.Value))
                .WithMessage(input => $"The active release's version of dataset {input.DatasetId} binds role {input.DimensionRoleId} into no members with an enabled binding.");
            RuleFor(input => input.CopyFromSourceId)
                .MustAsync(async (input, siblingId, cancellationToken) =>
                {
                    var sibling = (await sources.ListAsync(new { ReportSourceId = siblingId, keys.ReportId }, cancellationToken)).SingleOrDefault();
                    if (sibling?.DimensionRoleId is not { } siblingRole) return false;
                    var compared = await roles.ListAsync(new { DimensionRoleId = new[] { siblingRole, input.DimensionRoleId!.Value } }, cancellationToken);
                    return compared.Select(role => role.DimensionId).Distinct().Count() == 1;
                })
                .WithMessage(input => $"Source {input.CopyFromSourceId} is no bridge source of this report from the same dimension as role {input.DimensionRoleId}.")
                .When(input => input.CopyFromSourceId is not null && input.DimensionRoleId is not null);
        });
        When(input => input.MappingMethod == ReportMappingMethods.Direct, () =>
        {
            RuleFor(input => input.DimensionRoleId).Null().WithMessage("A direct source reads row keys from a field, so '{PropertyName}' must be empty.");
            RuleFor(input => input.CopyFromSourceId).Null().WithMessage("A direct source has no bridge to copy entries into, so '{PropertyName}' must be empty.");
            RuleFor(input => input.FieldKey)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("A direct source reads row keys from a field, so '{PropertyName}' is required.")
                .MustAsync(async (input, key, cancellationToken) =>
                    (await releaseDatasets.ActiveAsync(input.DatasetId, cancellationToken))?.Fields.GetValueOrDefault(key!) is { IsEnabled: true })
                .WithMessage(input => $"Field '{input.FieldKey}' is no enabled field of the active release's version of the dataset.");
        });
    }
}
