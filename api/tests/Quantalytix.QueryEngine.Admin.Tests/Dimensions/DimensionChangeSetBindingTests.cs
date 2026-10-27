using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Quantalytix.QueryEngine.Admin.Tests.Configuration;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms how the dimension endpoints bind what the web2 HTTP adapter sends, before anything reads the tenant: an operation no
/// member of the change-set union matches answers the 400 problem, so a malformed change set never surfaces as a 500, and the list's
/// <c>origin</c> query binds under its contract name. Runs in the startup collection, because starting the API sets FluentValidation's
/// process-wide name resolvers.
/// </summary>
[Collection(StartupCollection.Name)]
public sealed class DimensionChangeSetBindingTests : IDisposable
{
    /// <summary>FluentValidation's property name resolver before the API replaced it.</summary>
    private readonly Func<Type, MemberInfo, LambdaExpression, string> _propertyNames = ValidatorOptions.Global.PropertyNameResolver;

    /// <summary>FluentValidation's display name resolver before the API replaced it.</summary>
    private readonly Func<Type, MemberInfo, LambdaExpression, string> _displayNames = ValidatorOptions.Global.DisplayNameResolver;

    /// <summary>The API, hosted in memory.</summary>
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host.UseEnvironment(Environments.Development));

    /// <summary>An <c>op</c> that names no operation answers 400 with the error on the operation.</summary>
    /// <param name="operation">The operation's JSON.</param>
    /// <returns>A task that completes when the response is checked.</returns>
    [Theory]
    [InlineData("""{ "op": "renameDimension", "patch": { "displayName": "X" } }""")]
    [InlineData("""{ "patch": { "displayName": "X" } }""")]
    public async Task OperationNoMemberMatches_IsABadRequest(string operation)
    {
        var (status, problem) = await PreviewAsync($$"""{ "operations": [{{operation}}] }""");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemTypes.InvalidInput, (string)problem["type"]!);
    }

    /// <summary>An unknown origin answers 400 keyed by the query name the contract gives it.</summary>
    /// <returns>A task that completes when the response is checked.</returns>
    [Fact]
    public async Task ListWithAnUnknownOrigin_IsABadRequestOnOrigin()
    {
        using var client = await SignedInAsync();
        using var response = await client.GetAsync(new Uri("/api/tenants/acme/dimensions?origin=report", UriKind.Relative), TestContext.Current.CancellationToken);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["origin"], problem["errors"]!.AsObject().Select(entry => entry.Key));
    }

    /// <summary>Disposes the host and restores FluentValidation's name resolvers for the tests that run after this one.</summary>
    public void Dispose()
    {
        _factory.Dispose();
        ValidatorOptions.Global.PropertyNameResolver = _propertyNames;
        ValidatorOptions.Global.DisplayNameResolver = _displayNames;
    }

    /// <summary>Posts a preview body, signed in, and reads the problem.</summary>
    private async Task<(HttpStatusCode Status, JsonObject Problem)> PreviewAsync(string body)
    {
        using var client = await SignedInAsync();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/api/tenants/acme/dimensions/1/changes/preview", UriKind.Relative), content, TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject());
    }

    /// <summary>A client signed in to tenant <c>acme</c> through the Development token endpoint.</summary>
    private async Task<HttpClient> SignedInAsync()
    {
        var client = _factory.CreateClient();
        using var signIn = await client.PostAsJsonAsync("/development/token", new { tenantId = "acme" }, TestContext.Current.CancellationToken);
        var token = (await signIn.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken))!["accessToken"]!.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
