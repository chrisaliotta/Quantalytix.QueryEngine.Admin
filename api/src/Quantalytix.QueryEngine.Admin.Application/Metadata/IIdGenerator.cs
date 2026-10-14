namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// Allocates surrogate keys. Every metadata key is a plain BIGINT without auto-increment, so the API allocates IDs itself.
/// </summary>
public interface IIdGenerator
{
    /// <summary>Returns a new ID that is positive and below 2^53, so it stays exact as a JSON number in browsers.</summary>
    /// <returns>A new random ID.</returns>
    long NextId();
}
