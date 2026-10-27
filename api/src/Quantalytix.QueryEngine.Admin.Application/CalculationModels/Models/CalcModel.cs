using System.Diagnostics.CodeAnalysis;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The contract's <c>CalcModel</c>: a calculation model's own settings (<c>qe_calculation_model</c>).</summary>
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
