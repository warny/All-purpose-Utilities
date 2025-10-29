using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;
using Utils.OData.Generators;

namespace UtilsTest.OData;

/// <summary>
/// Tests covering the <see cref="ODataContext"/> base class and its accompanying source generator.
/// </summary>
[TestClass]
public class ODataContextGeneratorTests
{
    /// <summary>
    /// Gets or sets the active test context instance provided by MSTest.
    /// </summary>
    public TestContext? TestContext { get; set; }

    /// <summary>
    /// Ensures the <see cref="ODataContext"/> constructor can load metadata from a file path.
    /// </summary>
    [TestMethod]
    public void ConstructorLoadsMetadataFromFile()
    {
        string metadataPath = GetSampleMetadataPath();
        var context = new FileContext(metadataPath);

        Assert.IsNotNull(context.Metadata);
        Assert.IsNotNull(context.Metadata.DataServices);
        Assert.IsTrue(context.Metadata.DataServices!.Any());
    }

    /// <summary>
    /// Ensures the <see cref="ODataContext"/> constructor correctly reads metadata from a stream.
    /// </summary>
    [TestMethod]
    public void ConstructorLoadsMetadataFromStream()
    {
        string metadataPath = GetSampleMetadataPath();
        using var stream = File.OpenRead(metadataPath);
        var context = new StreamContext(stream);

        Assert.IsNotNull(context.Metadata);
        Assert.IsNotNull(context.Metadata.DataServices);
        Assert.IsTrue(context.Metadata.DataServices!.Any());
    }

    /// <summary>
    /// Validates that the source generator produces classes for OData entities defined in metadata.
    /// </summary>
    [TestMethod]
    public void GeneratorProducesEntityClasses()
    {
        string metadataPath = GetSampleMetadataPath();

        string source = $@"
using Utils.OData;

namespace GeneratedSample;

public partial class SampleGeneratedContext : ODataContext
{{
    public SampleGeneratedContext()
        : base(@""{metadataPath}"")
    {{
    }}
}}
";

        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var references = CreateMetadataReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName: "ODataGeneratorTests",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ODataEntityGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.IsNotNull(outputCompilation);
        Assert.AreEqual(0, diagnostics.Length, "Compilation produced diagnostics: {0}", string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())));

        var runResult = driver.GetRunResult();
        Assert.AreEqual(1, runResult.GeneratedTrees.Length);

        string generatedSource = runResult.GeneratedTrees[0].GetText().ToString();
        StringAssert.Contains(generatedSource, "class Product");
        StringAssert.Contains(generatedSource, "public int Id { get; set; }");
        StringAssert.Contains(generatedSource, "public string? Name { get; set; }");
    }

    /// <summary>
    /// Resolves the absolute path to the sample EDMX metadata used by the tests.
    /// </summary>
    /// <returns>The full file path to <c>Sample.edmx</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test base directory cannot be determined.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the metadata file is missing.</exception>
    private static string GetSampleMetadataPath()
    {
        string? baseDirectory = AppContext.BaseDirectory;
        if (baseDirectory is null)
        {
            throw new InvalidOperationException("Unable to resolve the base directory of the test run.");
        }

        string path = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "OData", "TestData", "Sample.edmx"));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Sample EDMX metadata file not found for tests.", path);
        }

        return path;
    }

    /// <summary>
    /// Creates the metadata references required to compile a temporary project for the source generator test.
    /// </summary>
    /// <returns>A read-only collection of metadata references.</returns>
    private static IReadOnlyCollection<MetadataReference> CreateMetadataReferences()
    {
        _ = typeof(object);
        _ = typeof(Enumerable);
        _ = typeof(Uri);
        _ = typeof(HttpClient);
        _ = typeof(Utils.OData.ODataContext);
        _ = typeof(System.ComponentModel.DataAnnotations.KeyAttribute);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToArray();

        return references;
    }

    /// <summary>
    /// Minimal concrete implementation of <see cref="ODataContext"/> used for tests.
    /// </summary>
    private sealed class FileContext : ODataContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileContext"/> class.
        /// </summary>
        /// <param name="path">Path to the EDMX metadata file.</param>
        public FileContext(string path)
            : base(path)
        {
        }
    }

    /// <summary>
    /// Minimal context that loads metadata from a stream for tests.
    /// </summary>
    private sealed class StreamContext : ODataContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StreamContext"/> class.
        /// </summary>
        /// <param name="stream">Stream containing EDMX metadata.</param>
        public StreamContext(Stream stream)
            : base(stream)
        {
        }
    }
}
