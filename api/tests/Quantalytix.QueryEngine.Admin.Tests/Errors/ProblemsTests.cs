using System.Text.Json;
using System.Text.Json.Nodes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Configuration;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Errors;

/// <summary>
/// Confirms that every problem serializes to the contract's one <c>Problem</c> shape (common.yaml, blueprint 01 "Problem body"):
/// <c>type</c>, <c>title</c>, <c>status</c> and <c>errors</c> always present, and <c>detail</c>, <c>violations</c> and
/// <c>traceId</c> present only for the statuses that carry them, never as <c>null</c>. Clients branch on these members, so a
/// stray <c>null</c> or a missing <c>errors</c> breaks the web HTTP adapters.
/// </summary>
public sealed class ProblemsTests
{
    /// <summary>The API's JSON options.</summary>
    private static readonly JsonSerializerOptions Options = ApiOptions();

    /// <summary>A 400 carries the input errors keyed by path, and nothing else beyond the required members.</summary>
    [Fact]
    public void Invalid_CarriesErrorsOnly()
    {
        var json = Serialize(Problems.Invalid(new Dictionary<string, string[]> { ["operations[3].memberKey"] = ["is taken"] }));

        Assert.Equal(["type", "title", "status", "errors"], Members(json));
        Assert.Equal(400, (int)json["status"]!);
        Assert.Equal("is taken", (string)json["errors"]!["operations[3].memberKey"]![0]!);
    }

    /// <summary>A 404 carries the title only, with an empty <c>errors</c>.</summary>
    [Fact]
    public void NotFound_IsTitleOnly()
    {
        var json = Serialize(Problems.OfStatus(404));

        Assert.Equal(["type", "title", "status", "errors"], Members(json));
        Assert.Equal(ProblemTypes.NotFound, (string)json["type"]!);
        Assert.Empty(json["errors"]!.AsObject());
    }

    /// <summary>A blocked delete is a 409 whose violations are the check's coded reasons and whose detail is its message.</summary>
    [Fact]
    public void DeleteBlocked_CarriesTheCheckAsViolations()
    {
        var blockers = new DeleteBlockers("Dimension 'Purpose'");
        blockers.AddNamed(DeleteBlockerCodes.ReferencedByMappingSet, "referenced by mapping sets", ["'purpose_map'"]);

        var json = Serialize(Problems.DeleteBlocked(blockers.Check()));

        Assert.Equal(["type", "title", "status", "detail", "errors", "violations"], Members(json));
        Assert.Equal(409, (int)json["status"]!);
        Assert.Equal("referenced_by_mapping_set", (string)json["violations"]![0]!["code"]!);
        Assert.Equal("Dimension 'Purpose' cannot be deleted: referenced by mapping sets 'purpose_map'.", (string)json["detail"]!);
    }

    /// <summary>A 422 carries the violations; a violation without a target leaves <c>target</c> out rather than writing null.</summary>
    [Fact]
    public void RulesViolated_CarriesViolationsWithoutNullTargets()
    {
        var json = Serialize(Problems.RulesViolated(null, [new RuleViolation { Code = "member_key_unique", Message = "Key 'CA' is used twice." }]));

        Assert.Equal(["type", "title", "status", "errors", "violations"], Members(json));
        Assert.Equal(["code", "message"], Members(json["violations"]![0]!.AsObject()));
    }

    /// <summary>A violation target writes its item id as a string, like every id.</summary>
    [Fact]
    public void ViolationTarget_WritesItsIdAsAString()
    {
        var violation = new RuleViolation { Code = "c", Message = "m", Target = new ViolationTarget { Area = "members", ItemId = "42" } };

        var json = JsonSerializer.SerializeToNode(violation, Options)!.AsObject();

        Assert.Equal("""{"area":"members","itemId":"42"}""", json["target"]!.ToJsonString());
    }

    /// <summary>A 500 carries the trace id.</summary>
    [Fact]
    public void Internal_CarriesTheTraceId()
    {
        var json = Serialize(Problems.Internal("00-abc-01"));

        Assert.Equal(["type", "title", "status", "errors", "traceId"], Members(json));
    }

    /// <summary>The problem as the API writes it.</summary>
    private static JsonObject Serialize(Problem problem)
    {
        return JsonSerializer.SerializeToNode(problem, Options)!.AsObject();
    }

    /// <summary>The names of an object's members, in order.</summary>
    private static string[] Members(JsonObject json)
    {
        return [.. json.Select(member => member.Key)];
    }

    /// <summary>Builds the options the API configures.</summary>
    private static JsonSerializerOptions ApiOptions()
    {
        var options = new JsonSerializerOptions();
        AdminControllersServiceCollectionExtensions.ConfigureJson(options);
        return options;
    }
}
