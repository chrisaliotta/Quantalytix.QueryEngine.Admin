using Microsoft.OpenApi;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Contract;

/// <summary>
/// Confirms what the conformance comparison catches, so a passing <see cref="ContractConformanceTests"/> means something: a
/// missing or extra property, a different type, a different nullability and a different status code are differences; a
/// contract schema that specializes a generic envelope with <c>allOf</c> conforms to the flat schema C# generates, and a
/// nullable reference written as <c>oneOf: [null, $ref]</c> conforms to one written as a nullable type.
/// </summary>
public sealed class OpenApiShapesTests
{
    /// <summary>Identical operations have no difference.</summary>
    [Fact]
    public void SameShape_Conforms()
    {
        Assert.Empty(OpenApiShapes.Differences(Operation(Object(("id", String()))), Operation(Object(("id", String())))));
    }

    /// <summary>A property on one side only is a difference, whichever side.</summary>
    [Fact]
    public void MissingAndExtraProperties_AreDifferences()
    {
        var differences = OpenApiShapes.Differences(Operation(Object(("id", String()), ("key", String()))), Operation(Object(("id", String()), ("name", String()))));

        Assert.Equal(["200.key: missing from the generated schema", "200.name: not in the contract"], differences);
    }

    /// <summary>A different type or nullability is a difference.</summary>
    [Fact]
    public void TypeAndNullability_AreCompared()
    {
        var differences = OpenApiShapes.Differences(
            Operation(Object(("id", String()), ("count", Integer()))),
            Operation(Object(("id", new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null }), ("count", String()))));

        Assert.Equal(["200.id: nullable False, generated True", "200.count: type Integer, generated String"], differences);
    }

    /// <summary>A different set of status codes is a difference.</summary>
    [Fact]
    public void StatusCodes_AreCompared()
    {
        var generated = Operation(Object(("id", String())));
        generated.Responses!["404"] = new OpenApiResponse { Description = "Not found." };

        Assert.Equal(["status codes: contract [200], generated [200, 404]"], OpenApiShapes.Differences(Operation(Object(("id", String()))), generated));
    }

    /// <summary>An <c>allOf</c> specialization whose later part narrows a property conforms to the merged flat schema.</summary>
    [Fact]
    public void AllOfSpecialization_ConformsToTheFlatSchema()
    {
        var envelope = Object(("report", Object(("versionId", String()))), ("live", new OpenApiSchema { Description = "Generic." }));
        var specialization = new OpenApiSchema { AllOf = [envelope, Object(("live", Object(("id", String()))))] };

        var flat = Object(("report", Object(("versionId", String()))), ("live", Object(("id", String()))));

        Assert.Empty(OpenApiShapes.Differences(Operation(specialization), Operation(flat)));
    }

    /// <summary>A nullable object written as <c>oneOf</c> with <c>null</c> conforms to a nullable object type.</summary>
    [Fact]
    public void NullableOneOf_ConformsToANullableType()
    {
        var contract = Object(("target", new OpenApiSchema { OneOf = [Object(("area", String())), new OpenApiSchema { Type = JsonSchemaType.Null }] }));
        var generated = Object(("target", new OpenApiSchema
        {
            Type = JsonSchemaType.Object | JsonSchemaType.Null,
            Properties = new Dictionary<string, IOpenApiSchema> { ["area"] = String() }
        }));

        Assert.Empty(OpenApiShapes.Differences(Operation(contract), Operation(generated)));
    }

    /// <summary>A GET answering 200 with a JSON body of the schema.</summary>
    private static OpenApiOperation Operation(IOpenApiSchema body)
    {
        return new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "OK.",
                    Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = body } }
                }
            }
        };
    }

    /// <summary>An object schema with the given properties.</summary>
    private static OpenApiSchema Object(params (string Name, IOpenApiSchema Schema)[] properties)
    {
        return new OpenApiSchema { Type = JsonSchemaType.Object, Properties = properties.ToDictionary(property => property.Name, property => property.Schema) };
    }

    /// <summary>A string schema.</summary>
    private static OpenApiSchema String()
    {
        return new OpenApiSchema { Type = JsonSchemaType.String };
    }

    /// <summary>An integer schema.</summary>
    private static OpenApiSchema Integer()
    {
        return new OpenApiSchema { Type = JsonSchemaType.Integer };
    }
}
