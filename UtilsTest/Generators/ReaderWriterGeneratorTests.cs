using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utils.IO.Serialization;
using Utils.IO.Serialization.Generators;

namespace UtilsTest.Generators;

/// <summary>Verifies validation, identity, and incremental behavior of the IO serializer generator.</summary>
[TestClass]
public sealed class ReaderWriterGeneratorTests
{
    /// <summary>Ensures init-only members produce one source diagnostic and no invalid generated C#.</summary>
    [TestMethod]
    public void InitOnlyProperty_ReportsDiagnosticWithoutGeneratedSource()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace Models;
            [GenerateReaderWriter]
            public partial class InitModel
            {
                [Field(0)] public int Value { get; init; }
            }
            """, out Compilation output);

        Diagnostic diagnostic = AssertHasSingleDiagnostic(result, "UIOSG010");
        StringAssert.Contains(diagnostic.Location.SourceTree!.GetRoot().FindNode(diagnostic.Location.SourceSpan).ToString(), "Value");
        Assert.AreEqual(0, result.GeneratedTrees.Length);
        Assert.IsFalse(output.GetDiagnostics().Any(item => item.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true));
    }

    /// <summary>Ensures homonymous and nested contracts have distinct hint, class, and method identities.</summary>
    [TestMethod]
    public void HomonymousTypes_ProduceUniqueCompilableIdentities()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace A { [GenerateReaderWriter] public class Customer { [Field(0)] public int Id { get; set; } } }
            namespace B { [GenerateReaderWriter] public class Customer { [Field(0)] public int Id { get; set; } } }
            namespace Nested {
                public class OuterA { [GenerateReaderWriter] public class Node { [Field(0)] public int Id { get; set; } } }
                public class OuterB { [GenerateReaderWriter] public class Node { [Field(0)] public int Id { get; set; } } }
            }
            """, out Compilation output);

        Assert.AreEqual(4, result.GeneratedTrees.Length);
        Assert.AreEqual(4, result.Results.SelectMany(item => item.GeneratedSources).Select(item => item.HintName).Distinct(StringComparer.Ordinal).Count());
        string generated = string.Join("\n", result.GeneratedTrees.Select(tree => tree.ToString()));
        Assert.AreEqual(4, CountOccurrences(generated, "public static class "));
        Assert.AreEqual(4, ExtractMethodNames(generated, "Read").Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(4, ExtractMethodNames(generated, "Write").Distinct(StringComparer.Ordinal).Count());
        AssertNoCompilationErrors(output);
    }

    /// <summary>Ensures multiple syntax declarations of one partial type produce one complete serializer.</summary>
    [TestMethod]
    public void PartialType_IsDeduplicatedBySymbol()
    {
        string first = """
            using Utils.IO.Serialization;
            namespace Models;
            [GenerateReaderWriter]
            public partial class SplitModel { [Field(0)] public int First { get; set; } }
            """;
        string second = """
            using Utils.IO.Serialization;
            namespace Models;
            public partial class SplitModel { [Field(1)] public int Second { get; set; } }
            """;
        GeneratorDriverRunResult result = RunGenerator([first, second], out Compilation output);

        Assert.AreEqual(1, result.GeneratedTrees.Length);
        string generated = result.GeneratedTrees[0].ToString();
        StringAssert.Contains(generated, "result.First");
        StringAssert.Contains(generated, "result.Second");
        AssertNoCompilationErrors(output);
    }

    /// <summary>Ensures containing generic arity contributes to nested serializer identities.</summary>
    [TestMethod]
    public void NestedGenericArities_ProduceUniqueGenericMethods()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace GenericModels;
            public class Container<T>
            {
                [GenerateReaderWriter]
                public class Item { [Field(0)] public T Value { get; set; } = default!; }
            }
            public class Container<TFirst, TSecond>
            {
                [GenerateReaderWriter]
                public class Item { [Field(0)] public TFirst Value { get; set; } = default!; }
            }
            """, out Compilation output);

        Assert.AreEqual(2, result.GeneratedTrees.Length);
        Assert.AreEqual(2, result.Results.SelectMany(item => item.GeneratedSources).Select(item => item.HintName).Distinct(StringComparer.Ordinal).Count());
        string generated = string.Join("\n", result.GeneratedTrees.Select(tree => tree.ToString()));
        StringAssert.Contains(generated, "<T>");
        StringAssert.Contains(generated, "<TFirst, TSecond>");
        AssertNoCompilationErrors(output);
    }

    /// <summary>Ensures ambiguous exact converters reject a contract without emitting secondary code.</summary>
    [TestMethod]
    public void AmbiguousConverter_ReportsDiagnosticWithoutSource()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace Models;
            public static class FirstConverter { public static Value ReadValue(this IReader reader) => new(); }
            public static class SecondConverter { public static Value ReadValue(this IReader reader) => new(); }
            public class Value { }
            [GenerateReaderWriter]
            public class Model { [Field(0)] public Value Value { get; set; } = new(); }
            """, out Compilation output);

        AssertHasSingleDiagnostic(result, "UIOSG005");
        Assert.AreEqual(0, result.GeneratedTrees.Length);
        Assert.IsFalse(output.GetDiagnostics().Any(item => item.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true));
    }

    /// <summary>Ensures an incompatible member codec produces the structured generator diagnostic.</summary>
    [TestMethod]
    public void InvalidWireCodec_ReportsDiagnosticWithoutSource()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace Models;
            [GenerateReaderWriter]
            public class Model { [Field(0), WireCodec(typeof(string))] public int Value { get; set; } }
            """, out _);

        AssertHasSingleDiagnostic(result, "UIOSG011");
        Assert.AreEqual(0, result.GeneratedTrees.Length);
    }

    /// <summary>Ensures an incompatible member framing produces the structured generator diagnostic.</summary>
    [TestMethod]
    public void InvalidWireFraming_ReportsDiagnosticWithoutSource()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace Models;
            [GenerateReaderWriter]
            public class Model { [Field(0), WireFraming(typeof(string))] public int Value { get; set; } }
            """, out _);

        AssertHasSingleDiagnostic(result, "UIOSG012");
        Assert.AreEqual(0, result.GeneratedTrees.Length);
    }

    /// <summary>Ensures a wire codec with only an internal parameterless constructor is rejected.</summary>
    [TestMethod]
    public void WireCodec_WithInternalParameterlessConstructor_IsRejected()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace Models;
            public sealed class Codec : IWireCodec<int>
            {
                internal Codec() { }
                public int Read(IReader reader) => 0;
                public void Write(IWriter writer, int value) { }
            }
            [GenerateReaderWriter]
            public class Model { [Field(0), WireCodec(typeof(Codec))] public int Value { get; set; } }
            """, out _);

        AssertHasSingleDiagnostic(result, "UIOSG011");
        Assert.AreEqual(0, result.GeneratedTrees.Length);
    }

    /// <summary>Ensures wire framing with only an internal parameterless constructor is rejected.</summary>
    [TestMethod]
    public void WireFraming_WithInternalParameterlessConstructor_IsRejected()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace Models;
            public sealed class Framing : IWireFraming
            {
                internal Framing() { }
                public WireFramingKind Kind => WireFramingKind.CodecOwned;
            }
            [GenerateReaderWriter]
            public class Model { [Field(0), WireFraming(typeof(Framing))] public int Value { get; set; } }
            """, out _);

        AssertHasSingleDiagnostic(result, "UIOSG012");
        Assert.AreEqual(0, result.GeneratedTrees.Length);
    }

    /// <summary>Ensures a codec with a public parameterless constructor remains accepted.</summary>
    [TestMethod]
    public void WireCodec_WithPublicParameterlessConstructor_IsAccepted()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace Models;
            public sealed class Codec : IWireCodec<int>
            {
                public Codec() { }
                public int Read(IReader reader) => 0;
                public void Write(IWriter writer, int value) { }
            }
            [GenerateReaderWriter]
            public class Model { [Field(0), WireCodec(typeof(Codec))] public int Value { get; set; } }
            """, out Compilation output);

        Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Id is "UIOSG011" or "UIOSG012"));
        Assert.AreEqual(1, result.GeneratedTrees.Length);
        AssertNoCompilationErrors(output);
    }

    /// <summary>Ensures nested generated contracts remain runtime-codec aware before using their generated fallback.</summary>
    [TestMethod]
    public void NestedGeneratedContract_UsesConfiguredCodecFallback()
    {
        GeneratorDriverRunResult result = RunGenerator("""
            using Utils.IO.Serialization;
            namespace Models;
            [GenerateReaderWriter]
            public class Child { [Field(0)] public int Value { get; set; } }
            [GenerateReaderWriter]
            public class Parent { [Field(0)] public Child Value { get; set; } = new(); }
            """, out Compilation output);

        string parentSource = result.GeneratedTrees.Select(tree => tree.ToString()).Single(source => source.Contains("global::Models.Parent", StringComparison.Ordinal));
        StringAssert.Contains(parentSource, "ReadConfiguredOr<global::Models.Child>");
        StringAssert.Contains(parentSource, "WriteConfiguredOr<global::Models.Child>");
        AssertNoCompilationErrors(output);
    }

    /// <summary>Runs the generator against one source document.</summary>
    private static GeneratorDriverRunResult RunGenerator(string source, out Compilation output) => RunGenerator([source], out output);

    /// <summary>Runs the generator against multiple source documents in one compilation.</summary>
    private static GeneratorDriverRunResult RunGenerator(IEnumerable<string> sources, out Compilation output)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.Preview);
        CSharpCompilation compilation = CSharpCompilation.Create("GeneratorTests",
            sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, parseOptions, $"Input{index}.cs")),
            GetReferences(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ReaderWriterGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out _);
        return driver.GetRunResult();
    }

    /// <summary>Gets framework and Utils.IO metadata references for an isolated Roslyn compilation.</summary>
    private static IEnumerable<MetadataReference> GetReferences()
    {
        string[] platformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return platformAssemblies.Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(FieldAttribute).Assembly.Location));
    }

    /// <summary>Asserts that generator execution produced one diagnostic with the requested identifier.</summary>
    private static Diagnostic AssertHasSingleDiagnostic(GeneratorDriverRunResult result, string id)
    {
        Diagnostic[] diagnostics = result.Diagnostics.Where(item => item.Id == id).ToArray();
        Assert.AreEqual(1, diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        return diagnostics[0];
    }

    /// <summary>Asserts that generated output compiles without C# errors.</summary>
    private static void AssertNoCompilationErrors(Compilation output)
    {
        Diagnostic[] errors = output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.AreEqual(0, errors.Length, string.Join(Environment.NewLine, errors.Select(item => item.ToString())));
    }

    /// <summary>Counts exact occurrences of a marker in generated source.</summary>
    private static int CountOccurrences(string value, string marker) => value.Split([marker], StringSplitOptions.None).Length - 1;

    /// <summary>Extracts generated method identifiers with a given prefix.</summary>
    private static IEnumerable<string> ExtractMethodNames(string source, string prefix) =>
        source.Split([' ', '(', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.StartsWith(prefix, StringComparison.Ordinal) && token.Length > prefix.Length);
}
