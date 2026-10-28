using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Builds release findings with the targets the contract's <c>ReleaseViolationTarget</c> describes.</summary>
public static class ReleaseFindings
{
    /// <summary>A finding on a manifest row, targeting its dataset.</summary>
    /// <param name="code">The rule code.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="datasetId">The row's dataset.</param>
    /// <returns>The violation.</returns>
    public static ReleaseViolation OnRow(string code, string message, long datasetId)
    {
        return new ReleaseViolation { Code = code, Message = message, DatasetId = datasetId, Target = Target(ReleaseTargetAreas.Dataset, datasetId) };
    }

    /// <summary>A target: a manifest row's dataset, an identity by its pin kind, or a report.</summary>
    /// <param name="area">A <c>ReleaseTargetArea</c>.</param>
    /// <param name="itemId">The dataset, identity or report.</param>
    /// <param name="itemKey">A report source's key; null otherwise.</param>
    /// <returns>The target.</returns>
    public static ViolationTarget Target(string area, long itemId, string? itemKey = null)
    {
        return new ViolationTarget { Area = area, ItemId = TemporaryIds.PresentStored(itemId), ItemKey = itemKey };
    }
}
