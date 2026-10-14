namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>Allocates the monotonic version number of a new version.</summary>
public static class VersionNumbers
{
    /// <summary>Returns the number after the highest existing one.</summary>
    /// <param name="existing">The version numbers already used by the owner's versions.</param>
    /// <returns>The highest existing number plus one, or 1 for the first version.</returns>
    public static int Next(IEnumerable<int> existing)
    {
        return existing.DefaultIfEmpty(0).Max() + 1;
    }
}
