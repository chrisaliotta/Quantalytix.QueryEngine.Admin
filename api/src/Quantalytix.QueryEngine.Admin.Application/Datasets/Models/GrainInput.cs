namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>A new grain of a draft dataset version.</summary>
public sealed record GrainInput : GrainUpdateInput
{
    /// <summary>Stable metadata name of the grain, such as <c>rowIdentity</c>; fixed once created.</summary>
    public required string GrainKey { get; init; }
}
