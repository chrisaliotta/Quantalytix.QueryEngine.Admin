using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a variable source: a variable of the route's version, a key free among its sources, an existing dataset, and a
/// source filter that compares dataset fields only. The field and measure keys resolve in the dataset version a release pins,
/// so the release checks them.
/// </summary>
public sealed class VariableSourceInputValidator : AbstractValidator<VariableSourceInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Checks the key against the route's model version.</param>
    /// <param name="variables">Reads the supplied variable.</param>
    /// <param name="sources">Reads the version's source keys.</param>
    /// <param name="datasets">Reads the dataset.</param>
    public VariableSourceInputValidator(
        ModelKeys keys,
        ITableGateway<VariableRecord> variables,
        ITableGateway<VariableSourceRecord> sources,
        ITableGateway<DatasetRecord> datasets)
    {
        RuleFor(input => input.VariableId)
            .MustAsync(async (variableId, cancellationToken) =>
                (await variables.ListAsync(new { VariableId = variableId, ModelVersionId = keys.VersionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Variable {input.VariableId} is not a variable of this model version.");
        RuleFor(input => input.SourceKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync((key, cancellationToken) => keys.KeyFreeAsync(sources, new { ModelVersionId = keys.VersionId, SourceKey = key },
                source => source.VariableSourceId, cancellationToken))
            .WithMessage(input => $"The model version already has a source with key '{input.SourceKey}'.");
        RuleFor(input => input.DatasetId)
            .MustAsync(async (datasetId, cancellationToken) => (await datasets.ListAsync(new { DatasetId = datasetId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dataset {input.DatasetId} does not exist.");
        RuleFor(input => input.ReportFieldKey).NotEmpty().MustFitVarchar(128);
        RuleFor(input => input.TimeFieldKey).NotEmpty().MustFitVarchar(128);
        RuleFor(input => input.MeasureKey).NotEmpty().MustFitVarchar(128);
        RuleFor(input => input.SourceFilterJson).MustBeFilterOver("field", "the dataset's fields");
    }
}
