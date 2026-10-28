using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Contract;

/// <summary>
/// The drift check between the agreed contract and the API (refactor plan, "Contract approach"): the bundled contract
/// (<c>api/openapi/contract/bundled.yaml</c>) and the document the build generates from C# (<c>api/openapi/admin-api.json</c>)
/// must describe exactly the same operations, paths, methods, status codes and body schemas (property names, types, nullability).
/// Operations are matched by method and path with parameter names ignored. A contract change lands in the contract first, then in
/// C#; an endpoint the contract lacks fails the build, so the two cannot drift.
/// </summary>
public sealed partial class ContractConformanceTests
{
    /// <summary>The HTTP methods operations use.</summary>
    private static readonly HttpMethod[] Methods = [HttpMethod.Get, HttpMethod.Put, HttpMethod.Post, HttpMethod.Delete, HttpMethod.Patch];

    /// <summary>The contract's operations, by method and normalized path.</summary>
    private static readonly Lazy<Task<IReadOnlyDictionary<string, OpenApiOperation>>> ContractOperations = new(() => OperationsAsync("bundled.yaml"));

    /// <summary>The generated operations, by method and normalized path.</summary>
    private static readonly Lazy<Task<IReadOnlyDictionary<string, OpenApiOperation>>> GeneratedOperations = new(() => OperationsAsync("admin-api.json"));

    /// <summary>Every contract operation is generated, and conforms.</summary>
    /// <returns>A task that completes when the operations are compared.</returns>
    [Fact]
    public async Task ContractOperations_AreGeneratedAndConform()
    {
        var contract = await ContractOperations.Value;
        var generated = await GeneratedOperations.Value;

        var problems = contract
            .SelectMany(entry => generated.TryGetValue(entry.Key, out var actual)
                ? OpenApiShapes.Differences(entry.Value, actual).Select(difference => $"{entry.Value.OperationId} ({entry.Key}): {difference}")
                : [$"{entry.Value.OperationId} ({entry.Key}): not generated"])
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>Every generated operation is in the contract.</summary>
    /// <returns>A task that completes when the operations are compared.</returns>
    [Fact]
    public async Task GeneratedOperations_AreInTheContract()
    {
        var contract = await ContractOperations.Value;
        var generated = await GeneratedOperations.Value;

        var unlisted = generated.Keys.Where(key => !contract.ContainsKey(key)).ToList();

        Assert.True(unlisted.Count == 0, string.Join(Environment.NewLine, unlisted.Select(key => $"{key} is not in the contract")));
    }

    /// <summary>The operations of a document in the output directory, keyed <c>METHOD /path/{}</c>.</summary>
    private static async Task<IReadOnlyDictionary<string, OpenApiOperation>> OperationsAsync(string fileName)
    {
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();
        await using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "OpenApi", fileName));
        var (document, diagnostic) = await OpenApiDocument.LoadAsync(stream, fileName.EndsWith(".json", StringComparison.Ordinal) ? "json" : "yaml", settings);
        Assert.True(diagnostic?.Errors.Count is null or 0, $"{fileName}: {string.Join("; ", diagnostic!.Errors.Select(error => error.Message))}");
        var operations = new Dictionary<string, OpenApiOperation>(StringComparer.Ordinal);
        foreach (var (path, item) in document!.Paths)
        {
            foreach (var method in Methods)
            {
                if (item.Operations?.TryGetValue(method, out var operation) == true)
                {
                    operations[Normalize($"{method.Method} {path}")] = operation;
                }
            }
        }

        return operations;
    }

    /// <summary>A <c>METHOD path</c> with every path parameter written <c>{}</c>, so parameter names do not matter.</summary>
    private static string Normalize(string operation)
    {
        return Parameter().Replace(operation, "{}");
    }

    /// <summary>A path parameter.</summary>
    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex Parameter();
}
