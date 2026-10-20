using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a calculation dimension route: a calculation dimension of the route's version, an existing dataset, an override source of
/// the version over that dataset, a classification into the calculation dimension's dimension (a <c>field</c> route for a scalar
/// one), and at most one default route per dimension and dataset and one override per dimension and source. Whether the field
/// produces the dimension in the dataset version a release pins is a release rule.
/// </summary>
public sealed class CalculationRouteInputValidator : AbstractValidator<CalculationRouteInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Names the route's report version and edited route.</param>
    /// <param name="calculationDimensions">Reads the routed dimension.</param>
    /// <param name="sources">Reads the overridden source.</param>
    /// <param name="datasets">Reads the dataset.</param>
    /// <param name="routes">Reads the version's other routes.</param>
    /// <param name="mappingSets">Reads a mapped route's mapping set.</param>
    /// <param name="bridges">Reads a bridge route's bridge.</param>
    public CalculationRouteInputValidator(
        VersionKeys keys,
        ITableGateway<CalculationDimensionRecord> calculationDimensions,
        ITableGateway<VariableSourceRecord> sources,
        ITableGateway<DatasetRecord> datasets,
        ITableGateway<CalculationRouteRecord> routes,
        ITableGateway<MappingSetRecord> mappingSets,
        ITableGateway<BridgeRecord> bridges)
    {
        RuleFor(input => input.CalculationDimensionId)
            .MustAsync(async (dimensionId, cancellationToken) => await DimensionAsync(dimensionId, cancellationToken) is not null)
            .WithMessage(input => $"Calculation dimension {input.CalculationDimensionId} is not a calculation dimension of this report version.");
        RuleFor(input => input.DatasetId)
            .MustAsync(async (datasetId, cancellationToken) => (await datasets.ListAsync(new { DatasetId = datasetId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dataset {input.DatasetId} does not exist.");
        RuleFor(input => input.VariableSourceId)
            .MustAsync(async (input, sourceId, cancellationToken) =>
                (await sources.ListAsync(new { VariableSourceId = sourceId, ReportVersionId = keys.VersionId, input.DatasetId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Source {input.VariableSourceId} is not a source of this report version over dataset {input.DatasetId}.")
            .When(input => input.VariableSourceId is not null);
        RuleFor(input => input.Classification).SetValidator(input => new ClassificationInputValidator(
            RouteKinds.Definition, async cancellationToken => (await DimensionAsync(input.CalculationDimensionId, cancellationToken))?.DimensionId, mappingSets, bridges));
        RuleFor(input => input).CustomAsync(async (input, context, cancellationToken) =>
        {
            var same = await routes.ListAsync(new { ReportVersionId = keys.VersionId, input.CalculationDimensionId }, cancellationToken);
            var clash = same.Where(route => route.CalculationBindingId != keys.ItemId).Any(route => input.VariableSourceId is { } sourceId
                ? route.VariableSourceId == sourceId
                : route.VariableSourceId is null && route.DatasetId == input.DatasetId);
            if (clash)
                context.AddFailure(input.VariableSourceId is null
                    ? $"Dataset {input.DatasetId} already has a default route for this calculation dimension."
                    : $"Source {input.VariableSourceId} already overrides the route of this calculation dimension.");
        });

        async Task<CalculationDimensionRecord?> DimensionAsync(long dimensionId, CancellationToken cancellationToken) =>
            (await calculationDimensions.ListAsync(new { CalculationDimensionId = dimensionId, ReportVersionId = keys.VersionId }, cancellationToken)).SingleOrDefault();
    }
}
