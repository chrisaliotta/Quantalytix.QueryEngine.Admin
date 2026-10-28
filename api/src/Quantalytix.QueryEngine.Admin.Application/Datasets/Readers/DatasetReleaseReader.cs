using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Reads which releases list datasets and pin their versions.</summary>
public sealed class DatasetReleaseReader(
    ITableGateway<ReleaseDatasetRecord> releaseDatasets,
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<DatabaseInfoRecord> databaseInfo)
{
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>Reads the release pins of one dataset, or of every dataset.</summary>
    /// <param name="datasetId">The dataset, or null for all.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The index.</returns>
    public async Task<DatasetReleaseIndex> ReadAsync(long? datasetId, CancellationToken cancellationToken)
    {
        var pins = await releaseDatasets.ListAsync(datasetId is { } id ? new { DatasetId = id } : null, cancellationToken);
        var listed = await releases.ListAsync(new { SemanticReleaseId = pins.Select(pin => pin.SemanticReleaseId).Distinct().ToArray() }, cancellationToken);
        var info = await databaseInfo.GetAsync(new { SingletonId }, cancellationToken);
        return new DatasetReleaseIndex(pins, listed, info.ActiveSemanticReleaseId);
    }
}
