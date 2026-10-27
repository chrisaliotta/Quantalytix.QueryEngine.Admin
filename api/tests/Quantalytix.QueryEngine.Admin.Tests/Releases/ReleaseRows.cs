using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>Builds the metadata rows the release rule tests seed, with every required column set to a valid default.</summary>
internal static class ReleaseRows
{
    /// <summary>The draft release every rule test validates.</summary>
    public const long SemanticReleaseId = 900;

    /// <summary>The branch role of dimension 100, which <see cref="Reader"/> names bindings by.</summary>
    public const long BranchRoleId = 41;

    /// <summary>The product role of dimension 200, which <see cref="Reader"/> names bindings by.</summary>
    public const long ProductRoleId = 42;

    /// <summary>The asOfDate field of dataset version 11, which <see cref="Reader"/> names a time binding by.</summary>
    public const long AsOfFieldId = 3;

    /// <summary>
    /// The manifest reader over a release's pins and the given bindings, naming them by the branch and product roles or, for a time
    /// binding, by the asOfDate field.
    /// </summary>
    /// <param name="manifest">The release's manifest rows.</param>
    /// <param name="datasets">The pinned datasets.</param>
    /// <param name="bindings">The bindings of the pinned dataset versions.</param>
    /// <returns>The reader.</returns>
    public static ReleaseManifestReader Reader(
        InMemoryTableGateway<ReleaseDatasetRecord> manifest, InMemoryTableGateway<DatasetRecord> datasets, params DimensionBindingRecord[] bindings)
    {
        var names = new BindingNames(
            new InMemoryTableGateway<DimensionRoleRecord>(DimensionRows.Role(BranchRoleId, "branch", 100), DimensionRows.Role(ProductRoleId, "product", 200)),
            new InMemoryTableGateway<FieldRecord>(DatasetRows.ColumnField(AsOfFieldId, 11, "asOfDate", "as_of_date", DataTypes.Date)));
        return new ReleaseManifestReader(manifest, datasets, new InMemoryTableGateway<DimensionBindingRecord>(bindings), names);
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
