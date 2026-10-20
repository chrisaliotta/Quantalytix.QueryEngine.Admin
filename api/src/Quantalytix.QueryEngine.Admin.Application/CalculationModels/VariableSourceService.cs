using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The variable sources of calculation model versions: the dataset measures that can supply each variable.</summary>
public sealed class VariableSourceService(ITableGateway<VariableSourceRecord> sources, ModelContentRemover remover, IIdGenerator ids)
    : ModelVersionRows<VariableSourceRecord, VariableSourceInput>(sources, ids)
{
    /// <summary>Deletes a source with the profile picks and override routes that name it; no report reads sources by ID.</summary>
    /// <param name="itemId">The source.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>Null: a source is always deleted.</returns>
    public override async Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken)
    {
        await remover.RemoveSourcesAsync([itemId], cancellationToken);
        return null;
    }

    /// <summary>The gateway key of a source.</summary>
    /// <param name="itemId">The source ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long itemId)
    {
        return new { VariableSourceId = itemId };
    }

    /// <summary>The model version a source belongs to.</summary>
    /// <param name="row">The source.</param>
    /// <returns>Its model version ID.</returns>
    protected override long VersionOf(VariableSourceRecord row)
    {
        return row.ModelVersionId;
    }

    /// <summary>The key a source is listed by.</summary>
    /// <param name="row">The source.</param>
    /// <returns>Its key.</returns>
    protected override string KeyOf(VariableSourceRecord row)
    {
        return row.SourceKey;
    }

    /// <summary>The source row an input describes; every source classifies its rows as of their observation time.</summary>
    /// <param name="itemId">The source ID.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override VariableSourceRecord ToRecord(long itemId, long versionId, VariableSourceInput input)
    {
        return new VariableSourceRecord
        {
            VariableSourceId = itemId,
            ModelVersionId = versionId,
            VariableId = input.VariableId,
            SourceKey = input.SourceKey,
            DatasetId = input.DatasetId,
            ReportFieldKey = input.ReportFieldKey,
            TimeFieldKey = input.TimeFieldKey,
            MeasureKey = input.MeasureKey,
            SourceFilterJson = input.SourceFilterJson,
            IsEnabled = input.IsEnabled
        };
    }
}
