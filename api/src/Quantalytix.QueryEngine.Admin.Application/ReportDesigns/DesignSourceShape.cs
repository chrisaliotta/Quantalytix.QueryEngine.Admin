using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>Whether a bridge's variable sources form one design source the design can edit as a whole.</summary>
internal static class DesignSourceShape
{
    /// <summary>Whether every variable source reads the same dataset, role, time field and filter.</summary>
    /// <param name="sources">The variable sources that classify through one bridge; at least one.</param>
    /// <returns>True when the sources differ only in their variable and measure.</returns>
    public static bool IsUniform(IReadOnlyList<VariableSourceRecord> sources)
    {
        var first = sources[0];
        return sources.All(source => source.DatasetId == first.DatasetId
            && source.ClassificationRoleId == first.ClassificationRoleId
            && source.TimeFieldKey == first.TimeFieldKey
            && source.SourceFilterJson == first.SourceFilterJson);
    }
}
