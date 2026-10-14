using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.SourceObjects;

/// <summary>
/// Confirms which tables and views datasets may query: every object of the tenant database except the query engine's own
/// <c>qe_*</c> tables and <c>vw_qe_*</c> views, sorted by name, matched by kind as well as name. The pickers, the dataset
/// validators and the <c>source_object_exists</c> rule all read this list, so a metadata table can never become a source.
/// </summary>
public sealed class SourceObjectServiceTests
{
    /// <summary>The service over a database holding fact tables, a view and metadata objects.</summary>
    private readonly SourceObjectService _service = new(new FixedSourceObjectCatalog(
        [
            Object("qe_field", SourceObjectKinds.Table),
            Object("loan_daily", SourceObjectKinds.Table),
            Object("vw_qe_dataset_field", SourceObjectKinds.View),
            Object("deposits_current", SourceObjectKinds.View),
            Object("Balance_Sheet", SourceObjectKinds.Table)
        ],
        new Dictionary<string, IReadOnlyList<PhysicalColumn>>()));

    /// <summary>Metadata objects are left out and the rest are sorted by name, ignoring case.</summary>
    /// <returns>A task that completes when the list is checked.</returns>
    [Fact]
    public async Task List_LeavesOutMetadataObjectsAndSortsByName()
    {
        var objects = await _service.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Balance_Sheet", "deposits_current", "loan_daily"], objects.Select(item => item.Name));
    }

    /// <summary>An object exists only with its own kind, and metadata objects never exist as sources.</summary>
    /// <param name="kind">The kind asked for.</param>
    /// <param name="name">The name asked for.</param>
    /// <param name="exists">Whether the service finds it.</param>
    /// <returns>A task that completes when the answer is checked.</returns>
    [Theory]
    [InlineData(SourceObjectKinds.Table, "loan_daily", true)]
    [InlineData(SourceObjectKinds.View, "loan_daily", false)]
    [InlineData(SourceObjectKinds.View, "deposits_current", true)]
    [InlineData(SourceObjectKinds.Table, "qe_field", false)]
    [InlineData(SourceObjectKinds.Table, "loan_monthly", false)]
    public async Task Exists_MatchesKindAndName(string kind, string name, bool exists)
    {
        Assert.Equal(exists, await _service.ExistsAsync(kind, name, TestContext.Current.CancellationToken));
    }

    /// <summary>A table or view of the given name and kind.</summary>
    private static SourceObject Object(string name, string kind)
    {
        return new SourceObject { Name = name, Kind = kind };
    }
}
