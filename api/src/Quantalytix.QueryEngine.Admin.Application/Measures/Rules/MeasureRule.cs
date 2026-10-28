using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>A dataset rule about measures, whose violations target the measure on the measures tab.</summary>
public abstract class MeasureRule : DatasetRule
{
    /// <summary>A violation about a measure.</summary>
    /// <param name="measure">The measure.</param>
    /// <param name="message">What is wrong.</param>
    /// <returns>The violation.</returns>
    protected RuleViolation Violation(MeasureRecord measure, string message)
    {
        return Violation(DatasetTabs.Measures, message, measure.MeasureId, measure.MeasureKey);
    }

    /// <summary>The measures in key order.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>The measures by key.</returns>
    protected static IEnumerable<MeasureRecord> ByKey(DatasetSnapshot snapshot)
    {
        return snapshot.Measures.OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal);
    }
}
