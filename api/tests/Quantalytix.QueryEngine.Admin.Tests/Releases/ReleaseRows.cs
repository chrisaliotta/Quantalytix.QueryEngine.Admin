using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>Builds the metadata rows the release rule tests seed, with every required column set to a valid default.</summary>
internal static class ReleaseRows
{
    /// <summary>The draft release every rule test validates.</summary>
    public const long ReleaseId = 900;

    /// <summary>A dataset.</summary>
    public static DatasetRecord Dataset(long datasetId, string datasetKey)
    {
        return new DatasetRecord { DatasetId = datasetId, DatasetKey = datasetKey, DisplayName = datasetKey, IsEnabled = true };
    }

    /// <summary>A version of a dataset over a table.</summary>
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
    public static ReleaseDatasetRecord Pin(long datasetId, long datasetVersionId)
    {
        return new ReleaseDatasetRecord { ReleaseId = ReleaseId, DatasetId = datasetId, DatasetVersionId = datasetVersionId };
    }

    /// <summary>An unpinned, enabled binding that groups by member; set kind-specific references with <c>with</c>.</summary>
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

    /// <summary>A version of a dimension, valid from 2026.</summary>
    public static DimensionVersionRecord DimensionVersion(long dimensionVersionId, long dimensionId, int versionNumber, string status)
    {
        return new DimensionVersionRecord
        {
            DimensionVersionId = dimensionVersionId,
            DimensionId = dimensionId,
            VersionNumber = versionNumber,
            Status = status,
            ValidFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}
