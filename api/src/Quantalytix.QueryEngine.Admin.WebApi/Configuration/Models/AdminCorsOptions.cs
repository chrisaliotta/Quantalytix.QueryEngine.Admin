namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>The browser origins allowed to call the API, bound from the <c>Cors</c> configuration section.</summary>
public sealed record AdminCorsOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "Cors";

    /// <summary>The allowed origins, such as <c>http://localhost:5174</c>; none by default.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];
}
