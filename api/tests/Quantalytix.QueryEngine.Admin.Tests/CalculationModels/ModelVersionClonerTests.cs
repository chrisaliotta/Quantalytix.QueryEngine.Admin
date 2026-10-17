using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms that cloning a model version copies every content row under a new ID and points each reference between the copies
/// at the copy: a source at its variable, a pick at its profile, variable and source, and a route at its calculation dimension
/// and overridden source, while datasets and canonical dimensions, which versions share, stay as they are. A clone that still
/// referenced the source version's rows would change the published version when the draft is edited.
/// </summary>
public sealed class ModelVersionClonerTests
{
    /// <summary>The draft the content is cloned into.</summary>
    private const long TargetVersionId = 42;

    /// <summary>Each table holds one row of version 41; the route overrides the region dimension for the GL source.</summary>
    [Fact]
    public async Task ClonedRows_ReferenceTheClones()
    {
        var variables = new InMemoryTableGateway<VariableRecord>(Variable(51, "balance"));
        var sources = new InMemoryTableGateway<VariableSourceRecord>(Source(71, 51, "gl_balance", 1));
        var profiles = new InMemoryTableGateway<SourceProfileRecord>(Profile(81, "actual"));
        var picks = new InMemoryTableGateway<SourceProfileVariableRecord>(Pick(81, 51, 71));
        var measures = new InMemoryTableGateway<CalculatedMeasureRecord>(Calculated(91, "yield", "[m.balance] * 2"));
        var dimensions = new InMemoryTableGateway<CalculationDimensionRecord>(CalculationDimension(101, "region", 600));
        var routes = new InMemoryTableGateway<CalculationRouteRecord>(Route(111, 101, 1, "region") with { VariableSourceId = 71 });
        var cloner = new ModelVersionCloner(variables, sources, profiles, picks, measures, dimensions, routes, new SequentialIdGenerator(1000));

        await cloner.CloneAsync(ModelVersionId, TargetVersionId, TestContext.Current.CancellationToken);

        var variable = Assert.Single(variables.All, row => row.ModelVersionId == TargetVersionId);
        var source = Assert.Single(sources.All, row => row.ModelVersionId == TargetVersionId);
        var profile = Assert.Single(profiles.All, row => row.ModelVersionId == TargetVersionId);
        var dimension = Assert.Single(dimensions.All, row => row.ModelVersionId == TargetVersionId);
        var route = Assert.Single(routes.All, row => row.ModelVersionId == TargetVersionId);
        Assert.Equal(variable.VariableId, source.VariableId);
        Assert.Contains(Pick(profile.SourceProfileId, variable.VariableId, source.VariableSourceId), picks.All);
        Assert.Equal((dimension.CalculationDimensionId, source.VariableSourceId, 1L), (route.CalculationDimensionId, route.VariableSourceId, route.DatasetId));
        Assert.Equal(600, dimension.DimensionId);
        Assert.Single(measures.All, row => row.ModelVersionId == TargetVersionId);
        Assert.Equal(2, picks.All.Count);
    }
}
