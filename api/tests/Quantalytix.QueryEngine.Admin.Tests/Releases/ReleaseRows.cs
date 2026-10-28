using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>Builds the metadata rows the release and report tests seed, with every required column set to a valid default.</summary>
internal static class ReleaseRows
{
    /// <summary>The release the report tests treat as active, and whose rows <see cref="Pin"/> builds.</summary>
    public const long SemanticReleaseId = 900;

    /// <summary>A release.</summary>
    /// <param name="semanticReleaseId">The release's ID.</param>
    /// <param name="releaseKey">The release's key.</param>
    /// <param name="status">A <c>publication_status</c> value; a published release was published at the epoch.</param>
    /// <returns>The release row.</returns>
    public static SemanticReleaseRecord Release(long semanticReleaseId, string releaseKey, string status)
    {
        return new SemanticReleaseRecord
        {
            SemanticReleaseId = semanticReleaseId,
            ReleaseKey = releaseKey,
            Status = status,
            CreatedAt = DateTime.UnixEpoch.AddDays(semanticReleaseId - SemanticReleaseId),
            PublishedAt = status is PublicationStatuses.Published or PublicationStatuses.Retired ? DateTime.UnixEpoch : null
        };
    }

    /// <summary>An assumption set; a historical one reads <see cref="SemanticReleaseId"/>.</summary>
    /// <param name="assumptionSetId">The set's ID.</param>
    /// <param name="key">The set's key, which is also its display name.</param>
    /// <param name="capturedAt">When a historical set was captured; null for the active set.</param>
    /// <returns>The set row.</returns>
    public static AssumptionSetRecord Set(long assumptionSetId, string key, DateTime? capturedAt)
    {
        return new AssumptionSetRecord
        {
            AssumptionSetId = assumptionSetId,
            AssumptionSetKey = key,
            DisplayName = key,
            Status = capturedAt is null ? AssumptionSetStatuses.Active : AssumptionSetStatuses.Historical,
            SemanticReleaseId = capturedAt is null ? null : SemanticReleaseId,
            CapturedAt = capturedAt,
            CreatedAt = capturedAt ?? DateTime.UnixEpoch
        };
    }

    /// <summary>A dataset.</summary>
    /// <param name="datasetId">The dataset's ID.</param>
    /// <param name="datasetKey">The dataset's key, which is also its display name.</param>
    /// <returns>The dataset row.</returns>
    public static DatasetRecord Dataset(long datasetId, string datasetKey)
    {
        return new DatasetRecord { DatasetId = datasetId, DatasetKey = datasetKey, DisplayName = datasetKey, IsEnabled = true };
    }

    /// <summary>A version of a dataset over a table.</summary>
    /// <param name="datasetVersionId">The version's ID.</param>
    /// <param name="datasetId">The owning dataset.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="status">A <c>publication_status</c> value.</param>
    /// <returns>The dataset version row.</returns>
    public static DatasetVersionRecord DatasetVersion(long datasetVersionId, long datasetId, int versionNumber, string status)
    {
        return new DatasetVersionRecord
        {
            CreatedAt = DateTime.UnixEpoch,
            DatasetVersionId = datasetVersionId,
            DatasetId = datasetId,
            VersionNumber = versionNumber,
            Status = status,
            SourceObjectKind = SourceObjectKinds.Table,
            SourceObject = "loan_daily",
            SupportsAggregate = true,
            SupportsDetail = true,
            ExpressionLanguageVersion = "1.0"
        };
    }

    /// <summary>A manifest row of <see cref="SemanticReleaseId"/>.</summary>
    /// <param name="datasetId">The pinned dataset.</param>
    /// <param name="datasetVersionId">The dataset version the row pins.</param>
    /// <returns>The manifest row.</returns>
    public static ReleaseDatasetRecord Pin(long datasetId, long datasetVersionId)
    {
        return new ReleaseDatasetRecord { SemanticReleaseId = SemanticReleaseId, DatasetId = datasetId, DatasetVersionId = datasetVersionId };
    }

    /// <summary>An unpinned, enabled binding; set kind-specific references with <c>with</c>.</summary>
    /// <param name="dimensionBindingId">The binding's ID.</param>
    /// <param name="datasetVersionId">The dataset version that owns the binding.</param>
    /// <param name="dimensionRoleId">The bound role, whose key rule messages name; null for a time binding.</param>
    /// <param name="dimensionId">The bound dimension, the role's template.</param>
    /// <param name="bindingKind">A <c>binding_kind</c> value.</param>
    /// <returns>The binding row.</returns>
    public static DimensionBindingRecord Binding(long dimensionBindingId, long datasetVersionId, long? dimensionRoleId, long dimensionId, string bindingKind)
    {
        return new DimensionBindingRecord
        {
            DimensionBindingId = dimensionBindingId,
            DatasetVersionId = datasetVersionId,
            DimensionRoleId = dimensionRoleId,
            DimensionId = dimensionId,
            BindingKind = bindingKind,
            RaggedBehavior = RaggedBehaviors.Null,
            DefaultPeriodAlignment = PeriodAlignments.Observation,
            UnmappedBehavior = UnmappedBehaviors.Null,
            IsRequired = false,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true
        };
    }

    /// <summary>A user-defined categorical, analytical dimension.</summary>
    /// <param name="dimensionId">The dimension's ID.</param>
    /// <param name="dimensionKey">The dimension's key, which is also its display name.</param>
    /// <returns>The dimension row.</returns>
    public static DimensionRecord Dimension(long dimensionId, string dimensionKey)
    {
        return new DimensionRecord
        {
            DimensionId = dimensionId,
            DimensionKey = dimensionKey,
            DisplayName = dimensionKey,
            DimensionKind = DimensionKinds.Categorical,
            DefinitionOrigin = DefinitionOrigins.User,
            IsRemovable = true,
            IsEnabled = true,
            DimensionPurpose = DimensionPurposes.Analytical
        };
    }

    /// <summary>A version of a dimension.</summary>
    /// <param name="dimensionVersionId">The version's ID.</param>
    /// <param name="dimensionId">The owning dimension.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="status">A <c>publication_status</c> value.</param>
    /// <returns>The dimension version row.</returns>
    public static DimensionVersionRecord DimensionVersion(long dimensionVersionId, long dimensionId, int versionNumber, string status)
    {
        return new DimensionVersionRecord
        {
            CreatedAt = DateTime.UnixEpoch,
            DimensionVersionId = dimensionVersionId,
            DimensionId = dimensionId,
            VersionNumber = versionNumber,
            Status = status
        };
    }
}
