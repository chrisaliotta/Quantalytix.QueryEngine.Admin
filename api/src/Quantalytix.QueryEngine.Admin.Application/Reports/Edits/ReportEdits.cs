using Quantalytix.QueryEngine.Admin.Application.Changes;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Applies the report operations in memory. Content is named by key; an operation that names a row, source, profile, calculation
/// dimension or style the report does not have is a conflict (409), because another author may have removed it. What a key
/// resolves to outside the report (metrics, dataset content, role members) is the report rules' concern.
/// </summary>
public static class ReportEdits
{
    /// <summary>The title of a conflict over content the report does not have.</summary>
    private const string MissingTitle = "Not in the report";

    /// <summary>Applies one operation.</summary>
    /// <param name="snapshot">The report as the earlier operations left it.</param>
    /// <param name="operation">The operation, whose input its validator has passed.</param>
    /// <param name="context">Allocates ids and takes refusals.</param>
    /// <returns>The report with the operation applied, or as it was when refused.</returns>
    public static ReportSnapshot Apply(ReportSnapshot snapshot, ReportOperation operation, ChangeContext context)
    {
        var content = operation switch
        {
            ReportUpdateSettingsOperation update => snapshot.Content with { Report = ReportSettingsColumns.Apply(snapshot.Content.Report, update.Patch) },
            ReportSaveRowOperation save => ReportRowEdits.Save(snapshot, save, context),
            ReportMoveRowOperation move => ReportRowEdits.Move(snapshot.Content, move, context),
            ReportDeleteRowOperation delete => ReportRowEdits.Delete(snapshot.Content, delete, context),
            ReportSaveSourceOperation save => ReportSourceEdits.Save(snapshot, save, context),
            ReportDeleteSourceOperation delete => ReportSourceEdits.Delete(snapshot.Content, delete, context),
            ReportSetSourceVariablesOperation set => ReportSourceEdits.SetVariables(snapshot.Content, set, context),
            ReportSetRowRulesOperation set => ReportSourceEdits.SetRowRules(snapshot.Content, set, context),
            ReportSetLeftOutOperation set => ReportSourceEdits.SetLeftOut(snapshot.Content, set, context),
            ReportSaveProfileOperation save => ReportContentEdits.SaveProfile(snapshot.Content, save, context),
            ReportDeleteProfileOperation delete => ReportContentEdits.DeleteProfile(snapshot.Content, delete, context),
            ReportSaveCalculationDimensionOperation save => ReportContentEdits.SaveDimension(snapshot, save, context),
            ReportDeleteCalculationDimensionOperation delete => ReportContentEdits.DeleteDimension(snapshot.Content, delete, context),
            ReportSetScenariosOperation set => ReportContentEdits.SetScenarios(snapshot, set, context),
            ReportSetVariableScopesOperation set => ReportContentEdits.SetScopes(snapshot.Content, set),
            ReportSetColumnsOperation set => ReportContentEdits.SetColumns(snapshot.Content, set, context),
            ReportSaveStyleOperation save => ReportContentEdits.SaveStyle(snapshot.Content, save),
            ReportDeleteStyleOperation delete => ReportContentEdits.DeleteStyle(snapshot.Content, delete, context),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Not a report operation.")
        };
        return context.Refused ? snapshot : snapshot with { Content = content };
    }

    /// <summary>The item of a report with a key; a conflict when the report has none.</summary>
    /// <typeparam name="T">The item's record.</typeparam>
    /// <param name="items">The report's items of the kind.</param>
    /// <param name="keyOf">An item's key.</param>
    /// <param name="key">The key the operation names.</param>
    /// <param name="noun">What the item is, such as <c>source</c>.</param>
    /// <param name="context">Takes the conflict.</param>
    /// <returns>The item, or null after raising the conflict.</returns>
    internal static T? Named<T>(IEnumerable<T> items, Func<T, string> keyOf, string key, string noun, ChangeContext context) where T : class
    {
        var item = items.FirstOrDefault(candidate => keyOf(candidate) == key);
        if (item is null) context.Conflict(MissingTitle, $"The report has no {noun} '{key}'.");
        return item;
    }
}
