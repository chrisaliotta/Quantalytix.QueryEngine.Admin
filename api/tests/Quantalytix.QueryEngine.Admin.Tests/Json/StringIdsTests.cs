using System.Text.Json;
using Quantalytix.QueryEngine.Admin.WebApi.Configuration;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Json;

/// <summary>
/// Confirms the API's id convention (blueprint 01, OI-04): with the API's JSON options, every surrogate id reads and writes as a
/// JSON string, a numeric id is refused so clients cannot drift back to numbers, and numbers that are not ids stay numbers.
/// </summary>
public sealed class StringIdsTests
{
    /// <summary>The API's JSON options, as MVC and the OpenAPI generator use them.</summary>
    private static readonly JsonSerializerOptions Options = ApiOptions();

    /// <summary>A DTO with every id shape, and numbers that are not ids.</summary>
    /// <param name="Id">The entity's own id.</param>
    /// <param name="DimensionId">A reference.</param>
    /// <param name="ParentMemberId">An optional reference.</param>
    /// <param name="FieldIds">A list of references.</param>
    /// <param name="SemanticRevision">A long that is not an id.</param>
    /// <param name="VersionNumber">An int that is not an id.</param>
    private sealed record Sample(long Id, long DimensionId, long? ParentMemberId, IReadOnlyList<long> FieldIds, long SemanticRevision, int VersionNumber);

    /// <summary>Ids, optional ids and id lists are written as strings; other numbers are written as numbers.</summary>
    [Fact]
    public void Write_IdsAsStrings_OtherNumbersAsNumbers()
    {
        var json = JsonSerializer.Serialize(new Sample(9007199254740991, 42, null, [1, 2], 7, 3), Options);

        Assert.Equal(
            """{"id":"9007199254740991","dimensionId":"42","parentMemberId":null,"fieldIds":["1","2"],"semanticRevision":7,"versionNumber":3}""",
            json);
    }

    /// <summary>A body with string ids reads back to the same values.</summary>
    [Fact]
    public void Read_StringIds()
    {
        var sample = JsonSerializer.Deserialize<Sample>(
            """{"id":"5","dimensionId":"42","parentMemberId":"8","fieldIds":["1"],"semanticRevision":7,"versionNumber":3}""",
            Options)!;

        Assert.Equal(new Sample(5, 42, 8, sample.FieldIds, 7, 3), sample);
        Assert.Equal([1L], sample.FieldIds);
    }

    /// <summary>A numeric id is refused, which MVC turns into a 400 keyed by the property's path.</summary>
    [Fact]
    public void Read_NumericId_IsRefused()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Sample>(
            """{"id":5,"dimensionId":"42","parentMemberId":null,"fieldIds":[],"semanticRevision":7,"versionNumber":3}""",
            Options));
    }

    /// <summary>Builds the options the API configures.</summary>
    private static JsonSerializerOptions ApiOptions()
    {
        var options = new JsonSerializerOptions();
        AdminControllersServiceCollectionExtensions.ConfigureJson(options);
        return options;
    }
}
