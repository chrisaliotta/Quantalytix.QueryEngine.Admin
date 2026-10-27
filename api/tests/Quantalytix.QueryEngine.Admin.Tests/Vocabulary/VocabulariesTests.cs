using System.Text.Json;
using System.Text.Json.Nodes;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.WebApi.Configuration;
using Quantalytix.QueryEngine.Admin.WebApi.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Vocabulary;

/// <summary>
/// Confirms the vocabulary hotspot rule (refactor-ownership: "each module adds its own vocabulary file, found by discovery") and
/// the contract's <c>Vocabularies</c> shape (OI-11): a vocabulary declared as a static field anywhere in the Application
/// assembly is found without being listed, every served key is unique, and the answer puts each vocabulary's terms, with
/// value, label and description, under its contract key.
/// </summary>
public sealed class VocabulariesTests
{
    /// <summary>Discovery finds vocabularies declared in their own files, and the validator lookup by name still works.</summary>
    [Fact]
    public void Catalog_DiscoversDeclaredVocabularies()
    {
        Assert.Contains(DimensionKinds.Definition, VocabularyCatalog.All);
        Assert.Same(PublicationStatuses.Definition, VocabularyCatalog.Find("publication_status"));
    }

    /// <summary>No two vocabularies or relationships claim the same key, so none hides another in the answer.</summary>
    [Fact]
    public void ServedKeys_AreUnique()
    {
        string[] keys = [.. VocabularyCatalog.All.Select(vocabulary => vocabulary.Key).OfType<string>(), .. VocabularyCatalog.Relations.Select(relation => relation.Key)];

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The answer is keyed by contract key, and each term carries its value, label and description.</summary>
    [Fact]
    public void Answer_KeysTermsByContractKey()
    {
        var options = new JsonSerializerOptions();
        AdminControllersServiceCollectionExtensions.ConfigureJson(options);

        var json = JsonSerializer.SerializeToNode(Vocabularies.FromCatalog(), options)!.AsObject();

        var term = json["versionStatus"]![0]!.AsObject();
        Assert.Equal(["value", "label", "description"], term.Select(member => member.Key));
        Assert.Equal("draft", (string)term["value"]!);
        Assert.Equal("Draft", (string)term["label"]!);
        Assert.Null(json["lookupKind"]);
    }
}
