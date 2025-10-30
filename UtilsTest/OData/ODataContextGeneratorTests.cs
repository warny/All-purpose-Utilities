using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
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

        string source = 
            $$"""
            using Utils.OData;

            namespace GeneratedSample;

            public partial class SampleGeneratedContext : ODataContext
            {
                public SampleGeneratedContext()
                    : base(@"{{metadataPath}}")
                {
                }
            }
            """;

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
    /// Ensures the source generator can download compressed EDMX metadata from an HTTP endpoint.
    /// </summary>
    [TestMethod]
    public void GeneratorLoadsCompressedMetadataFromHttp()
    {
        string metadataPath = GetSampleMetadataPath();
        string metadataUrl = StartCompressedMetadataServer(metadataPath, out var listener, out var serverTask);

        try
        {
            string source = 
                $$"""
                using Utils.OData;

                public partial class SampleHttpContext : ODataContext
                {
                    public SampleHttpContext()
                        : base(@"{{metadataUrl}}")
                    {
                    }
                }
                """;

            var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
            var references = CreateMetadataReferences();
            var compilation = CSharpCompilation.Create(
                assemblyName: "ODataGeneratorHttpTests",
                syntaxTrees: [syntaxTree],
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ODataEntityGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            Assert.IsNotNull(outputCompilation);
            Assert.AreEqual(0, diagnostics.Length, "Compilation produced diagnostics: {0}", string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())));

            var runResult = driver.GetRunResult();
            Assert.AreEqual(1, runResult.GeneratedTrees.Length);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            serverTask.GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Resolves the absolute path to the sample EDMX metadata used by the tests.
    /// </summary>
    /// <returns>The full file path to <c>Sample.edmx</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test base directory cannot be determined.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the metadata file is missing.</exception>
    private static string GetSampleMetadataPath()
    {
        string? baseDirectory = AppContext.BaseDirectory ?? throw new InvalidOperationException("Unable to resolve the base directory of the test run.");
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
    /// Starts a lightweight HTTP server that returns the sample metadata using gzip compression.
    /// </summary>
    /// <param name="metadataPath">The path to the EDMX file to serve.</param>
    /// <param name="listener">The listener that accepts HTTP connections.</param>
    /// <param name="serverTask">Task responsible for processing a single request.</param>
    /// <returns>The HTTP URL that exposes the metadata file.</returns>
    private static string StartCompressedMetadataServer(string metadataPath, out HttpListener listener, out Task serverTask)
    {
		ArgumentNullException.ThrowIfNull(metadataPath);

		listener = new HttpListener();
        int port = ReserveEphemeralPort();
        string prefix = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(prefix);
        listener.Start();

        serverTask = ServeCompressedMetadataAsync(listener, metadataPath);
        return $"{prefix}metadata";
    }

    /// <summary>
    /// Reserves a free TCP port on the local machine for the temporary HTTP server.
    /// </summary>
    /// <returns>An available TCP port number.</returns>
    private static int ReserveEphemeralPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Processes a single HTTP request and responds with the compressed metadata.
    /// </summary>
    /// <param name="listener">The listener accepting HTTP connections.</param>
    /// <param name="metadataPath">The path to the EDMX file to serve.</param>
    /// <returns>A task that completes once the request has been processed.</returns>
    private static async Task ServeCompressedMetadataAsync(HttpListener listener, string metadataPath)
    {
        try
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.StatusCode = 200;
            context.Response.AddHeader("Content-Encoding", "gzip");
            context.Response.ContentType = "application/xml";

            var responseStream = context.Response.OutputStream;
            using (var gzip = new GZipStream(responseStream, CompressionLevel.Fastest, leaveOpen: true))
            using (var fileStream = File.OpenRead(metadataPath))
            {
                await fileStream.CopyToAsync(gzip).ConfigureAwait(false);
            }

            responseStream.Flush();
            context.Response.Close();
        }
        catch (HttpListenerException)
        {
            return;
        }
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
