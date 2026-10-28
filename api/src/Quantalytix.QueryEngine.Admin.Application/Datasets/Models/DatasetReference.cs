namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>One item of a dataset version reading another.</summary>
/// <param name="TargetId">The item read.</param>
/// <param name="Tab">The <see cref="DatasetTabs"/> value of the reader.</param>
/// <param name="ItemId">The reader; null for the settings.</param>
/// <param name="ItemKey">The reader's key, or <c>Settings</c>.</param>
/// <param name="Relation">How the reader reads the item.</param>
/// <param name="BlocksDelete">Whether the reader names the item by id.</param>
public sealed record DatasetReference(long TargetId, string Tab, long? ItemId, string ItemKey, string Relation, bool BlocksDelete)
{
    /// <summary>The contract's dependent.</summary>
    /// <param name="presentId">Writes the reader's id.</param>
    /// <returns>The dependent.</returns>
    public DatasetDependent Dependent(Func<long, string> presentId)
    {
        return new DatasetDependent
        {
            Tab = Tab,
            ItemId = ItemId is { } id ? presentId(id) : null,
            ItemKey = ItemKey,
            Relation = Relation,
            BlocksDelete = BlocksDelete
        };
    }
}
