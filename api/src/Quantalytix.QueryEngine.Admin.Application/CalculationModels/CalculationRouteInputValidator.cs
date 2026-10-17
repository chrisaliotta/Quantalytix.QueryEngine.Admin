using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a calculation dimension route: a calculation dimension of the route's version, an existing dataset, an override
/// source of the version over that dataset, a route kind that suits the dimension, and at most one default route per
/// dimension and dataset and one override per dimension and source. Whether the field produces the dimension in the dataset
/// version a release pins is a release rule.
/// </summary>
public sealed class CalculationRouteInputValidator : AbstractValidator<CalculationRouteInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Names the route's model version and edited route.</param>
    /// <param name="calculationDimensions">Reads the routed dimension.</param>
    /// <param name="sources">Reads the overridden source.</param>
    /// <param name="datasets">Reads the dataset.</param>
    /// <param name="routes">Reads the version's other routes.</param>
    public CalculationRouteInputValidator(
        ModelKeys keys,
        ITableGateway<CalculationDimensionRecord> calculationDimensions,
        ITableGateway<VariableSourceRecord> sources,
        ITableGateway<DatasetRecord> datasets,
        ITableGateway<CalculationRouteRecord> routes)
    {
        RuleFor(input => input.CalculationDimensionId).CustomAsync(async (dimensionId, context, cancellationToken) =>
        {
            var dimension = (await calculationDimensions.ListAsync(new { CalculationDimensionId = dimensionId, ModelVersionId = keys.VersionId }, cancellationToken))
                .SingleOrDefault();
            if (dimension is null)
            {
                context.AddFailure($"Calculation dimension {dimensionId} is not a calculation dimension of this model version.");
                return;
            }

            var expected = dimension.ValueKind == CalculationValueKinds.Canonical ? RouteKinds.Semantic : RouteKinds.Field;
            if (context.InstanceToValidate.RouteKind != expected)
                context.AddFailure($"Calculation dimension '{dimension.DimensionKey}' is {dimension.ValueKind}, so its routes are {expected}.");
        });
        RuleFor(input => input.DatasetId)
            .MustAsync(async (datasetId, cancellationToken) => (await datasets.ListAsync(new { DatasetId = datasetId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dataset {input.DatasetId} does not exist.");
        RuleFor(input => input.VariableSourceId)
            .MustAsync(async (input, sourceId, cancellationToken) =>
                (await sources.ListAsync(new { VariableSourceId = sourceId, ModelVersionId = keys.VersionId, input.DatasetId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Source {input.VariableSourceId} is not a source of this model version over dataset {input.DatasetId}.")
            .When(input => input.VariableSourceId is not null);
        RuleFor(input => input.RouteKind).MustBeIn(RouteKinds.Definition);
        RuleFor(input => input.FieldKey).NotEmpty().MustFitVarchar(128);
        RuleFor(input => input).CustomAsync(async (input, context, cancellationToken) =>
        {
            var same = await routes.ListAsync(new { ModelVersionId = keys.VersionId, input.CalculationDimensionId }, cancellationToken);
            var clash = same.Where(route => route.CalculationBindingId != keys.ItemId).Any(route => input.VariableSourceId is { } sourceId
                ? route.VariableSourceId == sourceId
                : route.VariableSourceId is null && route.DatasetId == input.DatasetId);
            if (clash)
                context.AddFailure(input.VariableSourceId is null
                    ? $"Dataset {input.DatasetId} already has a default route for this calculation dimension."
                    : $"Source {input.VariableSourceId} already overrides the route of this calculation dimension.");
        });
    }
}
