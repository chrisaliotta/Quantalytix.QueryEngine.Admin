using System.Diagnostics.CodeAnalysis;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The contract's <c>ModelDocument</c>: the whole model, the read model of <c>GET</c> and the change set's state.</summary>
public sealed record ModelDocument : CalcModel
{
    /// <summary>A model's settings with its content and readers.</summary>
    /// <param name="model">The settings.</param>
    /// <param name="variables">The variables.</param>
    /// <param name="measures">The calculated measures.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="attributions">The explicit attribution rows.</param>
    /// <param name="effectiveDrivers">The drivers in effect per metric key.</param>
    /// <param name="usedBy">The reports that read the model.</param>
    [SetsRequiredMembers]
    public ModelDocument(
        CalcModel model,
        IReadOnlyList<ModelVariable> variables,
        IReadOnlyList<ModelMeasure> measures,
        IReadOnlyList<ModelParameterItem> parameters,
        IReadOnlyList<Attribution> attributions,
        IReadOnlyDictionary<string, IReadOnlyList<AttributionDriver>> effectiveDrivers,
        IReadOnlyList<ModelReportUse> usedBy)
        : base(model)
    {
        Variables = variables;
        Measures = measures;
        Parameters = parameters;
        Attributions = attributions;
        EffectiveDrivers = effectiveDrivers;
        UsedBy = usedBy;
    }

    /// <summary>The variables, by key.</summary>
    public required IReadOnlyList<ModelVariable> Variables { get; init; }

    /// <summary>The calculated measures, by key.</summary>
    public required IReadOnlyList<ModelMeasure> Measures { get; init; }

    /// <summary>The parameters, by key.</summary>
    public required IReadOnlyList<ModelParameterItem> Parameters { get; init; }

    /// <summary>The explicit attribution rows, by metric key.</summary>
    public required IReadOnlyList<Attribution> Attributions { get; init; }

    /// <summary>The drivers in effect per metric key, explicit or default; a variable without an attribution has no entry.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<AttributionDriver>> EffectiveDrivers { get; init; }

    /// <summary>The reports that read the model, by key.</summary>
    public required IReadOnlyList<ModelReportUse> UsedBy { get; init; }
}
