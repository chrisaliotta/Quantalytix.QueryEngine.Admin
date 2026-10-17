using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Answers whether a key is free in the route's model version. Variables and calculated measures share one key namespace,
/// because a formula names either through the same <c>[m.KEY]</c> reference; an edit may keep its own row's key.
/// </summary>
public sealed class ModelKeys(
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    IRequestRoute route)
{
    /// <summary>The model version the route names.</summary>
    public long VersionId => route.Id("versionId");

    /// <summary>The row the route edits, or null on a create route.</summary>
    public long? ItemId => route.OptionalId("itemId");

    /// <summary>Whether no other variable or calculated measure of the version has the key.</summary>
    /// <param name="key">The metric key.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the key is free.</returns>
    public async Task<bool> MetricKeyFreeAsync(string key, CancellationToken cancellationToken)
    {
        var sameVariables = await variables.ListAsync(new { ModelVersionId = VersionId, VariableKey = key }, cancellationToken);
        var sameMeasures = await calculatedMeasures.ListAsync(new { ModelVersionId = VersionId, MeasureKey = key }, cancellationToken);
        return sameVariables.All(variable => variable.VariableId == ItemId) && sameMeasures.All(measure => measure.CalculatedMeasureId == ItemId);
    }

    /// <summary>Whether no other row of the version's table has the key.</summary>
    /// <typeparam name="TRecord">A content row.</typeparam>
    /// <param name="rows">The content table.</param>
    /// <param name="filter">The version and key to match, such as <c>new { ModelVersionId = id, SourceKey = key }</c>.</param>
    /// <param name="idOf">The ID of a row.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the key is free.</returns>
    public async Task<bool> KeyFreeAsync<TRecord>(ITableGateway<TRecord> rows, object filter, Func<TRecord, long> idOf, CancellationToken cancellationToken)
        where TRecord : class
    {
        return (await rows.ListAsync(filter, cancellationToken)).All(row => idOf(row) == ItemId);
    }
}
