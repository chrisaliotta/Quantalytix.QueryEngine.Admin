using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Identifiers;

/// <summary>
/// Allocates random surrogate keys in [1, 2^53), the range the seed data uses. JavaScript numbers hold every integer in
/// that range exactly, and the space is large enough that collisions are negligible at metadata volumes.
/// </summary>
public sealed class IdGenerator : IIdGenerator
{
    /// <summary>2^53, so every ID is at most JavaScript's <c>Number.MAX_SAFE_INTEGER</c>.</summary>
    private const long ExclusiveMaximum = 1L << 53;

    /// <summary>Returns a new random ID.</summary>
    /// <returns>An ID that is positive and below 2^53.</returns>
    public long NextId()
    {
        return Random.Shared.NextInt64(1, ExclusiveMaximum);
    }
}
