using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Contract;

/// <summary>
/// The drift check between the agreed contract and the API (refactor plan, "Contract approach"): the bundled contract
/// (<c>api/openapi/contract/bundled.yaml</c>) and the document the build generates from C# (<c>api/openapi/admin-api.json</c>)
/// must describe the same operations, paths, methods, status codes and body schemas (property names, types, nullability).
/// Operations are matched by method and path with parameter names ignored. While modules migrate,
/// <see cref="ContractAllowList.NotImplemented"/> names the contract operations not built yet and
/// <see cref="ContractAllowList.Legacy"/> the old endpoints not removed yet; both lists must stay true, so they shrink to empty.
/// </summary>
public sealed partial class ContractConformanceTests
{
    /// <summary>The HTTP methods operations use.</summary>
    private static readonly HttpMethod[] Methods = [HttpMethod.Get, HttpMethod.Put, HttpMethod.Post, HttpMethod.Delete, HttpMethod.Patch];

    /// <summary>The contract's operations, by method and normalized path.</summary>
    private static readonly Lazy<Task<IReadOnlyDictionary<string, OpenApiOperation>>> ContractOperations = new(() => OperationsAsync("bundled.yaml"));

    /// <summary>The generated operations, by method and normalized path.</summary>
    private static readonly Lazy<Task<IReadOnlyDictionary<string, OpenApiOperation>>> GeneratedOperations = new(() => OperationsAsync("admin-api.json"));

    /// <summary>Every contract operation that is not allow-listed is generated, and conforms.</summary>
    /// <returns>A task that completes when the operations are compared.</returns>
    [Fact]
    public async Task ImplementedOperations_ConformToTheContract()
    {
        var contract = await ContractOperations.Value;
        var generated = await GeneratedOperations.Value;
        var notImplemented = NotImplemented();

        var problems = contract
            .Where(entry => !notImplemented.Contains(entry.Value.OperationId!))
            .SelectMany(entry => generated.TryGetValue(entry.Key, out var actual)
                ? OpenApiShapes.Differences(entry.Value, actual).Select(difference => $"{entry.Value.OperationId} ({entry.Key}): {difference}")
                : [$"{entry.Value.OperationId} ({entry.Key}): not generated"])
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>Every allow-listed operation is in the contract and does not conform yet; one that conforms must leave the list.</summary>
    /// <returns>A task that completes when the allow-list is checked.</returns>
    [Fact]
    public async Task NotImplementedList_NamesOnlyContractOperationsThatDoNotConform()
    {
        var contract = await ContractOperations.Value;
        var generated = await GeneratedOperations.Value;
        var notImplemented = NotImplemented();

        var unknown = notImplemented.Except(contract.Values.Select(operation => operation.OperationId!)).Select(id => $"{id} is not a contract operation");
        var conforming = contract
            .Where(entry => notImplemented.Contains(entry.Value.OperationId!))
            .Where(entry => generated.TryGetValue(entry.Key, out var actual) && OpenApiShapes.Differences(entry.Value, actual).Count == 0)
            .Select(entry => $"{entry.Value.OperationId} conforms; remove it from the list");
        var stale = unknown.Concat(conforming).ToList();

        Assert.True(stale.Count == 0, string.Join(Environment.NewLine, stale));
    }

    /// <summary>Every generated operation is in the contract or on the legacy list, and every legacy entry is still generated.</summary>
    /// <returns>A task that completes when the legacy list is checked.</returns>
    [Fact]
    public async Task GeneratedOperations_AreInTheContractOrLegacy()
    {
        var contract = await ContractOperations.Value;
        var generated = await GeneratedOperations.Value;
        var legacy = ContractAllowList.Legacy.Values.SelectMany(entries => entries).Select(Normalize).ToHashSet(StringComparer.Ordinal);

        var unlisted = generated.Keys.Where(key => !contract.ContainsKey(key) && !legacy.Contains(key)).Select(key => $"{key} is neither in the contract nor legacy");
        var stale = legacy.Where(key => !generated.ContainsKey(key) || contract.ContainsKey(key)).Select(key => $"{key} is no longer a legacy endpoint; remove it");
        var problems = unlisted.Concat(stale).ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>The allow-listed operation ids.</summary>
    private static HashSet<string> NotImplemented()
    {
        return ContractAllowList.NotImplemented.Values.SelectMany(ids => ids).ToHashSet(StringComparer.Ordinal);
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
