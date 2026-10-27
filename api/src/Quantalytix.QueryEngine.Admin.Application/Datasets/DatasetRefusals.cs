using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The conflicts the dataset module answers in more than one place.</summary>
internal static class DatasetRefusals
{
    /// <summary>The title of a refused DDL on an object the API did not create (OI-33).</summary>
    public const string NotManagedTitle = "Not a managed table";

    /// <summary>The title of a refused content edit or lifecycle step.</summary>
    public const string WrongStatusTitle = "Version status does not allow this";

    /// <summary>The conflict of a version whose status does not allow a step.</summary>
    /// <param name="version">The version.</param>
    /// <param name="allowed">The statuses that allow it, worded, such as <c>a draft</c>.</param>
    /// <returns>The refusal.</returns>
    public static Refusal WrongStatus(DatasetVersionRecord version, string allowed)
    {
        return Refusal.Conflict(WrongStatusTitle, $"Version v{version.VersionNumber} is {version.Status}; this needs {allowed}.");
    }
}
