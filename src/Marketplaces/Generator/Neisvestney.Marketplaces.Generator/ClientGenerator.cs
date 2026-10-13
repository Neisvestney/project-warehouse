using NJsonSchema.CodeGeneration.CSharp;
using NSwag;
using NSwag.CodeGeneration.CSharp;
using NSwag.CodeGeneration.OperationNameGenerators;

namespace Neisvestney.Marketplaces.Generator;

public static class ClientGenerator
{
    /// <summary>
    /// Settings shared by every marketplace client: the HttpClient is injected and owns the base address,
    /// DTOs are nullable System.Text.Json POCOs with read-only lists and <see cref="DateTimeOffset"/>.
    /// </summary>
    public static CSharpClientGeneratorSettings DefaultSettings(string @namespace, string className, string exceptionClass) => new()
    {
        ClassName = className,
        OperationNameGenerator = new SingleClientFromOperationIdOperationNameGenerator(),
        InjectHttpClient = true,
        DisposeHttpClient = false,
        UseBaseUrl = false,
        GenerateBaseUrlProperty = false,
        GenerateClientInterfaces = true,
        GenerateExceptionClasses = true,
        ExceptionClass = exceptionClass,
        GenerateOptionalParameters = false,
        ResponseArrayType = "System.Collections.Generic.IReadOnlyList",
        ParameterArrayType = "System.Collections.Generic.IEnumerable",
        CSharpGeneratorSettings =
        {
            Namespace = @namespace,
            GenerateNullableReferenceTypes = true,
            GenerateOptionalPropertiesAsNullable = true,
            RequiredPropertiesMustBeDefined = false,
            GenerateDataAnnotations = false,
            GenerateDefaultValues = false,
            ClassStyle = CSharpClassStyle.Poco,
            JsonLibrary = CSharpJsonLibrary.SystemTextJson,
            ArrayType = "System.Collections.Generic.IReadOnlyList",
            ArrayInstanceType = "System.Collections.Generic.List",
            DateTimeType = "System.DateTimeOffset",
            // money comes as `number` with any format or none; binary floating point loses kopecks
            NumberType = "decimal",
            NumberFloatType = "decimal",
            NumberDoubleType = "decimal",
        },
    };

    /// <param name="enumConverter">
    /// Converter type that replaces the <c>JsonStringEnumConverter&lt;T&gt;</c> NSwag puts on every enum property.
    /// That attribute outranks converters registered on the options and ignores <c>[EnumMember]</c>, so a
    /// value like <c>"Склад WB"</c> fails even when the spec lists it.
    /// </param>
    public static async Task GenerateAsync(string specPath, CSharpClientGeneratorSettings settings, string outputPath, string? enumConverter = null)
    {
        var document = await OpenApiDocument.FromFileAsync(specPath);
        var code = new CSharpClientGenerator(document, settings).GenerateFile();

        if (enumConverter is not null)
        {
            const string stringEnumConverter = "System.Text.Json.Serialization.JsonStringEnumConverter<";
            var count = code.Split(stringEnumConverter).Length - 1;
            code = code.Replace(stringEnumConverter, enumConverter + "<", StringComparison.Ordinal);
            Console.WriteLine($"  enum converter: {count} property attribute(s) -> {enumConverter}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, code);
        Console.WriteLine($"Generated {code.Length / 1024} KB -> {outputPath}");
    }
}
