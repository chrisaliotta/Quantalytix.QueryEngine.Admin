namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The conflict titles the dataset module answers in more than one place.</summary>
internal static class DatasetRefusals
{
    /// <summary>The title of a refused DDL on an object the API did not create (OI-33).</summary>
    public const string NotManagedTitle = "Not a managed table";

    /// <summary>The title of a refused change to a version of a frozen snapshot.</summary>
    public const string FrozenTitle = "Version is frozen";
}
