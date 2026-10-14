using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>An <see cref="IIdGenerator"/> that counts up from a start value, so a test can tell new IDs from seeded ones.</summary>
/// <param name="start">The first ID handed out.</param>
internal sealed class SequentialIdGenerator(long start) : IIdGenerator
{
    /// <summary>The next ID to hand out.</summary>
    private long _next = start;

    /// <summary>Returns the next ID.</summary>
    /// <returns>One more than the previous ID.</returns>
    public long NextId()
    {
        return _next++;
    }
}
