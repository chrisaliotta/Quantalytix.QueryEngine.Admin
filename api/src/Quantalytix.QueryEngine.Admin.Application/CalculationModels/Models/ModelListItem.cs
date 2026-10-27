using System.Diagnostics.CodeAnalysis;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The contract's <c>ModelListItem</c>: a model's settings with its metric counts and the reports that read it.</summary>
public sealed record ModelListItem : CalcModel
{
    /// <summary>A model's settings with its counts and readers.</summary>
    /// <param name="model">The settings.</param>
    /// <param name="variableCount">How many variables the model has.</param>
    /// <param name="measureCount">How many calculated measures the model has.</param>
    /// <param name="usedBy">The reports that read the model.</param>
    [SetsRequiredMembers]
    public ModelListItem(CalcModel model, int variableCount, int measureCount, IReadOnlyList<ModelReportUse> usedBy)
        : base(model)
    {
        VariableCount = variableCount;
        MeasureCount = measureCount;
        UsedBy = usedBy;
    }

    /// <summary>How many variables the model has.</summary>
    public required int VariableCount { get; init; }

    /// <summary>How many calculated measures the model has.</summary>
    public required int MeasureCount { get; init; }

    /// <summary>The reports that read the model, by key.</summary>
    public required IReadOnlyList<ModelReportUse> UsedBy { get; init; }
}
