using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Identifiers;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Identifiers;

/// <summary>
/// Confirms that allocated IDs are positive and at most JavaScript's <c>Number.MAX_SAFE_INTEGER</c> (2^53 - 1). A client that
/// parses an ID as a JavaScript number would silently round a larger ID to a different row's key.
/// </summary>
public sealed class IdGeneratorTests
{
    /// <summary>The largest integer a JavaScript number holds exactly.</summary>
    private const long MaxSafeInteger = (1L << 53) - 1;

    /// <summary>A large sample of IDs stays within [1, 2^53 - 1].</summary>
    [Fact]
    public void NextId_StaysPositiveAndBrowserSafe()
    {
        var generator = new IdGenerator();
        var ids = Enumerable.Range(0, 100_000).Select(_ => generator.NextId());
        Assert.All(ids, id => Assert.InRange(id, 1, MaxSafeInteger));
    }
}
