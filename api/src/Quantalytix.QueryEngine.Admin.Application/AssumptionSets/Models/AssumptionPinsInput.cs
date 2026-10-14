namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>An assumption set's complete list of pins, which replaces the current one.</summary>
public sealed record AssumptionPinsInput
{
    /// <summary>One pin per dimension.</summary>
    public required IReadOnlyList<AssumptionPinInput> Dimensions { get; init; }
}
