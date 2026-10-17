namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The source a profile picks for one variable.</summary>
public sealed record SourceProfilePick
{
    /// <summary>The variable.</summary>
    public required long VariableId { get; init; }

    /// <summary>The picked source, one of the variable's.</summary>
    public required long VariableSourceId { get; init; }
}
