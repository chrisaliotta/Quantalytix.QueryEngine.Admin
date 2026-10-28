using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The conflict every write of this module refuses a report bridge with: only its report writes it (B9).</summary>
public static class BridgeRefusals
{
    /// <summary>The title of the refusal.</summary>
    public const string OwnedByReportTitle = "Owned by a report";

    /// <summary>Refuses a write to a report bridge.</summary>
    /// <param name="bridge">The report bridge.</param>
    /// <param name="owner">The report source that owns it.</param>
    /// <returns>The refusal.</returns>
    public static Refusal OwnedByReport(BridgeRecord bridge, BridgeOwner owner)
    {
        return Refusal.Conflict(OwnedByReportTitle, OwnedByReportDetail(bridge, owner));
    }

    /// <summary>The detail of the refusal, which the delete check's blocker repeats.</summary>
    /// <param name="bridge">The report bridge.</param>
    /// <param name="owner">The report source that owns it.</param>
    /// <returns>The sentence.</returns>
    public static string OwnedByReportDetail(BridgeRecord bridge, BridgeOwner owner)
    {
        return $"Bridge '{bridge.BridgeKey}' belongs to source '{owner.SourceKey}' of report '{owner.ReportKey}'; edit its rules in the report editor.";
    }
}
