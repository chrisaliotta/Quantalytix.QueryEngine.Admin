using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a variable source: a well-formed variable key, a key free among the version's sources, an existing dataset, a
/// classification into the report's dimension and a source filter that compares dataset fields only. Whether the model's
/// published version has the variable is a publication rule, and whether the dataset keys resolve in the dataset version a
/// release pins is a release rule.
/// </summary>
public sealed class VariableSourceInputValidator : AbstractValidator<VariableSourceInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Names the route's report version and edited source.</param>
    /// <param name="sources">Reads the version's source keys.</param>
    /// <param name="datasets">Reads the dataset.</param>
    /// <param name="reportVersions">Reads the route's report version.</param>
    /// <param name="reports">Reads its report, which names the report's dimension.</param>
    /// <param name="mappingSets">Reads a mapped classification's mapping set.</param>
    /// <param name="bridges">Reads a bridge classification's bridge.</param>
    /// <param name="roles">Reads a bridge classification's role.</param>
    public VariableSourceInputValidator(
        VersionKeys keys,
        ITableGateway<VariableSourceRecord> sources,
        ITableGateway<DatasetRecord> datasets,
        ITableGateway<ReportVersionRecord> reportVersions,
        ITableGateway<ReportRecord> reports,
        ITableGateway<MappingSetRecord> mappingSets,
        ITableGateway<BridgeRecord> bridges,
        ITableGateway<DimensionRoleRecord> roles)
    {
        RuleFor(input => input.VariableKey).MustBeKey(128);
        RuleFor(input => input.SourceKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync((key, cancellationToken) => keys.KeyFreeAsync(sources, new { ReportVersionId = keys.VersionId, SourceKey = key },
                source => source.VariableSourceId, cancellationToken))
            .WithMessage(input => $"The report version already has a source with key '{input.SourceKey}'.");
        RuleFor(input => input.DatasetId)
            .MustAsync(async (datasetId, cancellationToken) => (await datasets.ListAsync(new { DatasetId = datasetId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dataset {input.DatasetId} does not exist.");
        RuleFor(input => input.Classification).SetValidator(new ClassificationInputValidator(async cancellationToken =>
        {
            var version = await reportVersions.GetAsync(new { ReportVersionId = keys.VersionId }, cancellationToken);
            return (await reports.GetAsync(new { version.ReportId }, cancellationToken)).ReportDimensionId;
        }, mappingSets, bridges, roles));
        RuleFor(input => input.TimeFieldKey).NotEmpty().MustFitVarchar(128);
        RuleFor(input => input.MeasureKey).NotEmpty().MustFitVarchar(128);
        RuleFor(input => input.SourceFilterJson).MustBeFilterOver("field", "the dataset's fields");
    }
}
