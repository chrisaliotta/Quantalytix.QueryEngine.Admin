using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The variable sources of report versions: the dataset measures that can supply each model variable, with their classification.</summary>
public sealed class VariableSourceService(ITableGateway<VariableSourceRecord> sources, ReportContentRemover remover, IIdGenerator ids)
    : ReportVersionRows<VariableSourceRecord, VariableSourceInput>(sources, ids)
{
    /// <summary>Deletes a source with the profile picks that name it.</summary>
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

    /// <summary>The report version a source belongs to.</summary>
    /// <param name="row">The source.</param>
    /// <returns>Its report version ID.</returns>
    protected override long VersionOf(VariableSourceRecord row)
    {
        return row.ReportVersionId;
    }

    /// <summary>The key a source is listed by.</summary>
    /// <param name="row">The source.</param>
    /// <returns>Its key.</returns>
    protected override string KeyOf(VariableSourceRecord row)
    {
        return row.SourceKey;
    }

    /// <summary>The source row an input describes.</summary>
    /// <param name="itemId">The source ID.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override VariableSourceRecord ToRecord(long itemId, long versionId, VariableSourceInput input)
    {
        return new VariableSourceRecord
        {
            VariableSourceId = itemId,
            ReportVersionId = versionId,
            VariableKey = input.VariableKey,
            SourceKey = input.SourceKey,
            DatasetId = input.DatasetId,
            ClassificationKind = input.Classification.Kind,
            ClassificationFieldKey = input.Classification.FieldKey,
            ClassificationRoleId = input.Classification.RoleId,
            MappingSetId = input.Classification.MappingSetId,
            BridgeId = input.Classification.BridgeId,
            TimeFieldKey = input.TimeFieldKey,
            MeasureKey = input.MeasureKey,
            SourceFilterJson = input.SourceFilterJson,
            IsEnabled = input.IsEnabled
        };
    }
}
