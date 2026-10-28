namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The contract's <c>SetCapturePreview</c>: what a capture would pin and leave out, and the release it would record.</summary>
public sealed record SetCapturePreview
{
    /// <summary>Analytical dimensions a capture pins.</summary>
    public required int Dimensions { get; init; }

    /// <summary>Mapping sets a capture pins.</summary>
    public required int MappingSets { get; init; }

    /// <summary>Analytical bridges a capture pins.</summary>
    public required int Bridges { get; init; }

    /// <summary>Report dimensions a capture leaves out.</summary>
    public required int LeftOutDimensions { get; init; }

    /// <summary>Bridges into report dimensions a capture leaves out.</summary>
    public required int LeftOutBridges { get; init; }

    /// <summary>The active release, which the capture records.</summary>
    public required long ReleaseId { get; init; }
}
