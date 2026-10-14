using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// The tenant's semantic revision in <c>qe_database_info</c>. The query engine keys its metadata cache by it, so content
/// that reaches queries without a release activation must advance it.
/// </summary>
public sealed class SemanticRevision(ITableGateway<DatabaseInfoRecord> databaseInfo, TimeProvider time)
{
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>Adds one to the semantic revision and stamps the row's update time.</summary>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>A task that completes when the revision is written.</returns>
    public async Task AdvanceAsync(CancellationToken cancellationToken)
    {
        var info = await databaseInfo.GetAsync(new { SingletonId }, cancellationToken);
        var advanced = info with { SemanticRevision = info.SemanticRevision + 1, UpdatedAt = time.GetUtcNow().UtcDateTime };
        await databaseInfo.UpdateAsync(advanced, cancellationToken);
    }
}
