namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>allocation_mode</c> vocabulary: how a bridge allocates a source member across its target members.</summary>
public static class AllocationModes
{
    /// <summary>Each source maps to at most one target; the allocation weight must be 1.</summary>
    public const string None = "none";

    /// <summary>A source maps to several targets with nonnegative weights that sum to 1.</summary>
    public const string Partition = "partition";

    /// <summary>A source participates fully in several targets; target results are intentionally non-additive.</summary>
    public const string Replicate = "replicate";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "allocation_mode",
        Description = "How a bridge allocates a source member across its target members.",
        Values =
        [
            new() { Value = None, Description = "Each source maps to at most one target; the allocation weight must be 1." },
            new() { Value = Partition, Description = "A source maps to several targets with nonnegative weights that sum to 1." },
            new() { Value = Replicate, Description = "A source participates fully in several targets; target results are intentionally non-additive." }
        ]
    };
}
