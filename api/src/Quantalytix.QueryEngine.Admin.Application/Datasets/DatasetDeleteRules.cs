using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// What blocks deleting a dataset, a version, or an item inside a version (D10, OI-17), shared by the delete checks and the deletes.
/// Only drafts and validated versions that no release pins can go; an item goes when nothing names it by id.
/// </summary>
public static class DatasetDeleteRules
{
    /// <summary>The delete check of a dataset: every version is open, and no release lists the dataset.</summary>
    /// <param name="dataset">The dataset.</param>
    /// <param name="versions">Its versions.</param>
    /// <param name="releases">The release pins of the dataset.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Dataset(DatasetRecord dataset, IEnumerable<DatasetVersionRecord> versions, DatasetReleaseIndex releases)
    {
        var blockers = new DeleteBlockers($"Dataset '{dataset.DatasetKey}'");
        blockers.AddNamed(
            DeleteBlockerCodes.HasPublishedVersions,
            "it has published or retired versions",
            versions.Where(version => !IsOpen(version)).OrderBy(version => version.VersionNumber).Select(version => $"v{version.VersionNumber} ({version.Status})"));
        blockers.AddNamed(DeleteBlockerCodes.ReferencedByRelease, "listed by releases", releases.Listing(dataset.DatasetId).Select(release => $"'{release.ReleaseKey}'"));
        return blockers.Check();
    }

    /// <summary>The delete check of a version: it is open, and no release pins it.</summary>
    /// <param name="version">The version.</param>
    /// <param name="releases">The release pins of its dataset.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Version(DatasetVersionRecord version, DatasetReleaseIndex releases)
    {
        var blockers = new DeleteBlockers($"Version v{version.VersionNumber}");
        if (!IsOpen(version)) blockers.Add(DeleteBlockerCodes.PublishedVersion, $"it is {version.Status}");
        blockers.AddNamed(DeleteBlockerCodes.ReferencedByRelease, "pinned by releases", releases.Pinning(version.DatasetVersionId).Select(release => $"'{release.ReleaseKey}'"));
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

    /// <summary>Whether a version is open: a draft or validated (OI-23).</summary>
    /// <param name="version">The version.</param>
    /// <returns>True for a draft or a validated version.</returns>
    public static bool IsOpen(DatasetVersionRecord version)
    {
        return version.Status is PublicationStatuses.Draft or PublicationStatuses.Validated;
    }
}
