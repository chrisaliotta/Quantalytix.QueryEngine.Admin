using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Reads, writes and deletes the child rows of dataset versions: fields, grains, bindings and measures with their own
/// children, binding and measure rows included. Each table is written with one statement, parents before children, and
/// deleted children first, because StarRocks runs no UPDATE or DELETE inside a transaction.
/// </summary>
public sealed class DatasetVersionContentStore(
    ITableGateway<FieldRecord> fields,
    ITableGateway<DatasetGrainRecord> grains,
    ITableGateway<DatasetGrainFieldRecord> grainFields,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<MeasureRecord> measures,
    ITableGateway<MeasureComponentRecord> components,
    ITableGateway<MeasureDependencyRecord> dependencies,
    ITableGateway<MeasureTimePolicyRecord> timePolicies)
{
    /// <summary>Reads every child row of the given dataset versions.</summary>
    /// <param name="versionIds">The dataset versions.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The versions' content.</returns>
    public async Task<DatasetVersionContent> LoadAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionIds };
        var versionGrains = await grains.ListAsync(versionFilter, cancellationToken);
        var versionBindings = await bindings.ListAsync(versionFilter, cancellationToken);
        var versionMeasures = await measures.ListAsync(versionFilter, cancellationToken);
        var grainFilter = new { GrainId = versionGrains.Select(grain => grain.GrainId).ToArray() };
        var bindingFilter = new { BindingId = versionBindings.Select(binding => binding.BindingId).ToArray() };
        var measureFilter = new { MeasureId = versionMeasures.Select(measure => measure.MeasureId).ToArray() };
        return new DatasetVersionContent
        {
            Fields = await fields.ListAsync(versionFilter, cancellationToken),
            Grains = versionGrains,
            GrainFields = await grainFields.ListAsync(grainFilter, cancellationToken),
            Bindings = versionBindings,
            TimeBindings = await timeBindings.ListAsync(bindingFilter, cancellationToken),
            Measures = versionMeasures,
            Components = await components.ListAsync(measureFilter, cancellationToken),
            Dependencies = await dependencies.ListAsync(measureFilter, cancellationToken),
            TimePolicies = await timePolicies.ListAsync(measureFilter, cancellationToken)
        };
    }

    /// <summary>Writes content rows, parents before the children that reference them.</summary>
    /// <param name="content">The rows to write, already pointing at their dataset version.</param>
    /// <param name="cancellationToken">Cancels the remaining writes.</param>
    /// <returns>A task that completes when every row is written.</returns>
    public async Task InsertAsync(DatasetVersionContent content, CancellationToken cancellationToken)
    {
        await fields.InsertManyAsync(content.Fields, cancellationToken);
        await grains.InsertManyAsync(content.Grains, cancellationToken);
        await grainFields.InsertManyAsync(content.GrainFields, cancellationToken);
        await bindings.InsertManyAsync(content.Bindings, cancellationToken);
        await timeBindings.InsertManyAsync(content.TimeBindings, cancellationToken);
        await measures.InsertManyAsync(content.Measures, cancellationToken);
        await components.InsertManyAsync(content.Components, cancellationToken);
        await dependencies.InsertManyAsync(content.Dependencies, cancellationToken);
        await timePolicies.InsertManyAsync(content.TimePolicies, cancellationToken);
    }

    /// <summary>Deletes every child row of the given dataset versions, children before the parents they reference.</summary>
    /// <param name="versionIds">The dataset versions, whose own rows the caller deletes afterwards.</param>
    /// <param name="cancellationToken">Cancels the remaining deletes.</param>
    /// <returns>A task that completes when the content is deleted.</returns>
    public async Task DeleteAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        var content = await LoadAsync(versionIds, cancellationToken);
        var versionFilter = new { DatasetVersionId = versionIds };
        var measureFilter = new { MeasureId = content.Measures.Select(measure => measure.MeasureId).ToArray() };
        await grainFields.DeleteAsync(new { GrainId = content.Grains.Select(grain => grain.GrainId).ToArray() }, cancellationToken);
        await grains.DeleteAsync(versionFilter, cancellationToken);
        await timePolicies.DeleteAsync(measureFilter, cancellationToken);
        await dependencies.DeleteAsync(measureFilter, cancellationToken);
        await components.DeleteAsync(measureFilter, cancellationToken);
        await measures.DeleteAsync(versionFilter, cancellationToken);
        await timeBindings.DeleteAsync(new { BindingId = content.Bindings.Select(binding => binding.BindingId).ToArray() }, cancellationToken);
        await bindings.DeleteAsync(versionFilter, cancellationToken);
        await fields.DeleteAsync(versionFilter, cancellationToken);
    }
}
