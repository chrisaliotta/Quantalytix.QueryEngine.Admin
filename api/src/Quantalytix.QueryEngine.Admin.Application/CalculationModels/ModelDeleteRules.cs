using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>What blocks deleting a calculation model (C8): every report over it, enabled or not, because a report's model is fixed.</summary>
public static class ModelDeleteRules
{
    /// <summary>The delete check of a model, with one <c>referenced_by_report</c> blocker per report.</summary>
    /// <param name="model">The model row.</param>
    /// <param name="readers">The reports over the model, by key.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Model(CalculationModelRecord model, IEnumerable<ReportRecord> readers)
    {
        var blockers = new DeleteBlockers($"Calculation model '{model.ModelKey}'");
        foreach (var report in readers) blockers.Add(DeleteBlockerCodes.ReferencedByReport, $"report '{report.ReportKey}' reads it");
        return blockers.Check();
    }
}
