using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>Builds the metadata rows the release rule tests seed, with every required column set to a valid default.</summary>
internal static class ReleaseRows
{
    /// <summary>The draft release every rule test validates.</summary>
    public const long ReleaseId = 900;

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

    /// <summary>A manifest row of <see cref="ReleaseId"/>.</summary>
    /// <param name="datasetId">The pinned dataset.</param>
    /// <param name="datasetVersionId">The dataset version the row pins.</param>
    /// <returns>The manifest row.</returns>
    public static ReleaseDatasetRecord Pin(long datasetId, long datasetVersionId)
    {
        return new ReleaseDatasetRecord { ReleaseId = ReleaseId, DatasetId = datasetId, DatasetVersionId = datasetVersionId };
    }

    /// <summary>An unpinned, enabled binding that groups by member; set kind-specific references with <c>with</c>.</summary>
    /// <param name="bindingId">The binding's ID.</param>
    /// <param name="datasetVersionId">The dataset version that owns the binding.</param>
    /// <param name="displayName">The binding's display name, which rule messages name.</param>
    /// <param name="dimensionId">The bound dimension.</param>
    /// <param name="bindingKind">A <c>binding_kind</c> value.</param>
    /// <returns>The binding row.</returns>
    public static DimensionBindingRecord Binding(long bindingId, long datasetVersionId, string displayName, long dimensionId, string bindingKind)
    {
        return new DimensionBindingRecord
        {
            BindingId = bindingId,
            DatasetVersionId = datasetVersionId,
            DisplayName = displayName,
            DimensionId = dimensionId,
            BindingKind = bindingKind,
            GroupingMode = GroupingModes.Member,
            RaggedBehavior = RaggedBehaviors.Null,
            DefaultPeriodAlignment = PeriodAlignments.Observation,
            UnmappedBehavior = UnmappedBehaviors.Null,
            IsRequired = false,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true
        };
    }

    /// <summary>A user-defined categorical dimension.</summary>
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
            IsEnabled = true
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
            DimensionVersionId = dimensionVersionId,
            DimensionId = dimensionId,
            VersionNumber = versionNumber,
            Status = status
        };
    }
}
