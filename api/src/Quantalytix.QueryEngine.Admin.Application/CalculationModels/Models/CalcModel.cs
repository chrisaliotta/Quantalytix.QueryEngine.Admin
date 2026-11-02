using System.Diagnostics.CodeAnalysis;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The contract's <c>CalcModel</c>: a calculation model's identity with one version's settings (<c>qe_calculation_model_version</c>).
/// </summary>
public record CalcModel
{
    /// <summary>Creates the settings.</summary>
    public CalcModel()
    {
    }

    /// <summary>Copies the settings of a model, for the read models that extend them.</summary>
    /// <param name="model">The settings.</param>
    [SetsRequiredMembers]
    protected CalcModel(CalcModel model)
    {
        Id = model.Id;
        VersionId = model.VersionId;
        SnapshotId = model.SnapshotId;
        IsLive = model.IsLive;
        Key = model.Key;
        DisplayName = model.DisplayName;
        CurrencyCode = model.CurrencyCode;
        CalendarKey = model.CalendarKey;
        FormulaLanguage = model.FormulaLanguage;
        AttributionMethod = model.AttributionMethod;
        IsEnabled = model.IsEnabled;
    }

    /// <summary>The model's id.</summary>
    public required long Id { get; init; }

    /// <summary>The version whose settings these are.</summary>
    public required long VersionId { get; init; }

    /// <summary>The snapshot the version belongs to.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Whether the version is live, and so editable; a frozen version is read-only.</summary>
    public required bool IsLive { get; init; }

    /// <summary>The model's key, fixed after creation (C9).</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The ISO 4217 currency of the model's monetary figures, such as <c>USD</c>.</summary>
    public required string CurrencyCode { get; init; }

    /// <summary>The calendar the model's periods follow; <c>gregorian</c>.</summary>
    public required string CalendarKey { get; init; }

    /// <summary>A <c>formula_language_version</c> value: the grammar of the model's formulas and attribution expressions.</summary>
    public required string FormulaLanguage { get; init; }

    /// <summary>An <c>attribution_method</c> value: <c>shapley</c> or <c>sequential</c>.</summary>
    public required string AttributionMethod { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}
