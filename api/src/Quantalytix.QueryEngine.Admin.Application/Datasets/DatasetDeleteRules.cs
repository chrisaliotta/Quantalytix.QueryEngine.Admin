using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// What blocks deleting a dataset or an item inside its live version (D10), shared by the delete checks and the deletes. A dataset goes
/// with its live version when no frozen snapshot holds a version of it; an item goes when nothing names it by id.
/// </summary>
public static class DatasetDeleteRules
{
    /// <summary>The delete check of a dataset: no frozen snapshot holds a version of it.</summary>
    /// <param name="dataset">The dataset.</param>
    /// <param name="frozenSnapshotKeys">The keys of the frozen snapshots holding a version of it.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Dataset(DatasetRecord dataset, IEnumerable<string> frozenSnapshotKeys)
    {
        var blockers = new DeleteBlockers($"Dataset '{dataset.DatasetKey}'");
        blockers.AddFrozenSnapshots(frozenSnapshotKeys);
        return blockers.Check();
    }

    /// <summary>The delete check of an item of a version: nothing of the version names it by id.</summary>
    /// <param name="snapshot">The version.</param>
    /// <param name="itemId">The field, grain, binding or measure.</param>
    /// <param name="subject">The item, worded, such as <c>Field 'balance'</c>.</param>
    /// <param name="code">The item kind's <see cref="DatasetItemBlockerCodes"/> value.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Item(DatasetSnapshot snapshot, long itemId, string subject, string code)
    {
        var blockers = new DeleteBlockers(subject);
        AddReaders(blockers, snapshot, itemId, code);
        return blockers.Check();
    }

    /// <summary>The delete check of a binding: it is not required, and nothing of the version names it by id.</summary>
    /// <param name="snapshot">The version.</param>
    /// <param name="binding">The binding.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Binding(DatasetSnapshot snapshot, DimensionBindingRecord binding)
    {
        var blockers = new DeleteBlockers($"Binding '{Bindings.DatasetBindingNames.Of(snapshot)[binding.DimensionBindingId]}'");
        if (binding.IsRequired) blockers.Add(DatasetItemBlockerCodes.RequiredBinding, "it is a required binding of the dataset");
        AddReaders(blockers, snapshot, binding.DimensionBindingId, DatasetItemBlockerCodes.BindingInUse);
        return blockers.Check();
    }

    /// <summary>Adds the reason naming what reads an item by id, when anything does.</summary>
    private static void AddReaders(DeleteBlockers blockers, DatasetSnapshot snapshot, long itemId, string code)
    {
        blockers.AddNamed(code, "it is read by", DatasetDependents.Blocking(snapshot, itemId).Select(reference => $"{reference.ItemKey} ({reference.Relation})"));
    }
}
