using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Linq.Expressions;
using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Quantalytix.QueryEngine.Admin.Tests.Configuration;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Errors;

/// <summary>
/// Confirms that responses no action writes, a 401 challenge and an unmatched route, also leave as the contract's
/// <c>Problem</c> (blueprint 01: "One shape for every error"), not as an empty body or ASP.NET's <c>ProblemDetails</c>. Runs
/// in the startup collection, because starting the API sets FluentValidation's process-wide name resolvers.
/// </summary>
[Collection(StartupCollection.Name)]
public sealed class ProblemPipelineTests : IDisposable
{
    /// <summary>FluentValidation's property name resolver before the API replaced it.</summary>
    private readonly Func<Type, MemberInfo, LambdaExpression, string> _propertyNames = ValidatorOptions.Global.PropertyNameResolver;

    /// <summary>FluentValidation's display name resolver before the API replaced it.</summary>
    private readonly Func<Type, MemberInfo, LambdaExpression, string> _displayNames = ValidatorOptions.Global.DisplayNameResolver;

    /// <summary>The API, hosted in memory.</summary>
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host.UseEnvironment(Environments.Development));

    /// <summary>A tenant route without a bearer token answers the 401 problem.</summary>
    [Fact]
    public async Task MissingToken_AnswersTheUnauthorizedProblem()
    {
        var (status, problem) = await GetAsync("/api/tenants/acme/dimensions", signedIn: false);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal(ProblemTypes.Unauthorized, (string)problem["type"]!);
        Assert.Empty(problem["errors"]!.AsObject());
    }

    /// <summary>A signed-in request to a path no route matches answers the 404 problem.</summary>
    [Fact]
    public async Task UnmatchedRoute_AnswersTheNotFoundProblem()
    {
        var (status, problem) = await GetAsync("/api/no/such/route", signedIn: true);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal(ProblemTypes.NotFound, (string)problem["type"]!);
    }

    /// <summary>Disposes the host and restores FluentValidation's name resolvers for the tests that run after this one.</summary>
    public void Dispose()
    {
        _factory.Dispose();
        ValidatorOptions.Global.PropertyNameResolver = _propertyNames;
        ValidatorOptions.Global.DisplayNameResolver = _displayNames;
    }

    /// <summary>Sends a GET, signed in through the Development token endpoint when asked, and reads the problem body.</summary>
    private async Task<(HttpStatusCode Status, JsonObject Problem)> GetAsync(string path, bool signedIn)
    {
        using var client = _factory.CreateClient();
        if (signedIn)
        {
            using var signIn = await client.PostAsJsonAsync("/development/token", new { tenantId = "acme" }, TestContext.Current.CancellationToken);
            var token = (await signIn.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken))!["accessToken"]!.GetValue<string>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(ProblemWriter.ContentType, response.Content.Headers.ContentType?.MediaType);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        return (response.StatusCode, body);
    }
}
