using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// What blocks deleting a calculation model (C8): every live report over it, enabled or not, because a report's model is fixed, and
/// every frozen snapshot that holds a version of it.
/// </summary>
public static class ModelDeleteRules
{
    /// <summary>The delete check of a model, with one <c>referenced_by_report</c> blocker per report.</summary>
    /// <param name="model">The model row.</param>
    /// <param name="readers">The live reports over the model, by key.</param>
    /// <param name="frozenSnapshotKeys">The keys of the frozen snapshots holding a version of the model.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Model(CalculationModelRecord model, IEnumerable<VersionedReport> readers, IEnumerable<string> frozenSnapshotKeys)
    {
        var blockers = new DeleteBlockers($"Calculation model '{model.ModelKey}'");
        foreach (var report in readers) blockers.Add(DeleteBlockerCodes.ReferencedByReport, $"report '{report.ReportKey}' reads it");
        blockers.AddFrozenSnapshots(frozenSnapshotKeys);
        return blockers.Check();
    }
}
