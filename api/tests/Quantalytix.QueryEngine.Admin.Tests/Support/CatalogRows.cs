using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.Support;

/// <summary>Builds the catalog rows the report and dimension tests seed around their subject, with every required column set to a valid default.</summary>
internal static class CatalogRows
{
    /// <summary>A dataset.</summary>
    /// <param name="datasetId">The dataset's ID.</param>
    /// <param name="datasetKey">The dataset's key, which is also its display name.</param>
    /// <returns>The dataset row.</returns>
    public static DatasetRecord Dataset(long datasetId, string datasetKey)
    {
        return new DatasetRecord { DatasetId = datasetId, DatasetKey = datasetKey, DisplayName = datasetKey };
    }

    /// <summary>A binding of a dimension in a dataset version.</summary>
    /// <param name="dimensionBindingId">The binding's ID.</param>
    /// <param name="datasetVersionId">The dataset version.</param>
    /// <param name="dimensionRoleId">The bound role; null for a time binding.</param>
    /// <param name="dimensionId">The bound dimension.</param>
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
}
