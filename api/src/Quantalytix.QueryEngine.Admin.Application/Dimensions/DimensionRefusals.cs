using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The conflicts every write of the dimensions module shares: report-owned dimensions (M8) and locked profiles (M11).</summary>
public static class DimensionRefusals
{
    /// <summary>The title of the refusal of a locked dimension.</summary>
    public const string ReadOnlyTitle = "Read-only";

    /// <summary>Refuses a write to a report dimension, which only its report edits.</summary>
    /// <param name="dimension">The report dimension.</param>
    /// <returns>The refusal.</returns>
    public static Refusal OwnedByReport(DimensionRecord dimension)
    {
        return Refusal.Conflict("Owned by a report", $"Dimension '{dimension.DimensionKey}' holds a report's rows; edit it in the report editor.");
    }

    /// <summary>Refuses a write to a dimension whose profile is locked.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="profile">Its locked profile.</param>
    /// <returns>The refusal.</returns>
    public static Refusal ReadOnly(DimensionRecord dimension, DimensionProfile profile)
    {
        return Refusal.Conflict(ReadOnlyTitle, ReadOnlyDetail(dimension, profile));
    }

    /// <summary>The detail of the refusal of a locked dimension.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="profile">Its locked profile.</param>
    /// <returns>The sentence.</returns>
    public static string ReadOnlyDetail(DimensionRecord dimension, DimensionProfile profile)
    {
        return $"'{dimension.DisplayName}' is managed by {profile.Owner}.";
    }
}
