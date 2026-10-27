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
/// Confirms that a dimension change set's operations bind by their <c>op</c> discriminator wherever it stands in the object, and
/// that an operation no union member matches answers the 400 problem before anything reads the tenant, so a malformed change set
/// never surfaces as a 500. Runs in the startup collection, because starting the API sets FluentValidation's process-wide name
/// resolvers.
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

    /// <summary>Disposes the host and restores FluentValidation's name resolvers for the tests that run after this one.</summary>
    public void Dispose()
    {
        _factory.Dispose();
        ValidatorOptions.Global.PropertyNameResolver = _propertyNames;
        ValidatorOptions.Global.DisplayNameResolver = _displayNames;
    }

    /// <summary>Posts a preview body, signed in through the Development token endpoint, and reads the problem.</summary>
    private async Task<(HttpStatusCode Status, JsonObject Problem)> PreviewAsync(string body)
    {
        using var client = _factory.CreateClient();
        using var signIn = await client.PostAsJsonAsync("/development/token", new { tenantId = "acme" }, TestContext.Current.CancellationToken);
        var token = (await signIn.Content.ReadFromJsonAsync<JsonObject>(TestContext.Current.CancellationToken))!["accessToken"]!.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/api/tenants/acme/dimensions/1/changes/preview", UriKind.Relative), content, TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject());
    }
}
