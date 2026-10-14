namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>provider_id</c> vocabulary: the registered analytical provider that serves a tenant.</summary>
public static class ProviderIds
{
    /// <summary>StarRocks, reached over the MySQL protocol.</summary>
    public const string StarRocks = "starrocks";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "provider_id",
        Description = "The registered analytical provider that serves a tenant.",
        Values =
        [
            new() { Value = StarRocks, Description = "StarRocks, reached over the MySQL protocol." }
        ]
    };
}
