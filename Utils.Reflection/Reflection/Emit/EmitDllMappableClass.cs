using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.Loader;
using System.Threading;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Utility class for dynamically generating types that map to DLL functions.
/// This class emits new types at runtime, which implement a specified interface.
/// The methods of the interface are mapped to corresponding unmanaged functions
/// using the DllMapper class.
/// </summary>
/// <remarks>
/// <b>Security warning:</b> the generated C# source is built by concatenating type, method and
/// parameter names obtained through reflection on the target interface. CLR metadata names are far
/// less constrained than C# lexical identifiers, so an interface sourced from an untrusted or
/// dynamically generated assembly could inject arbitrary C# into the compiled/loaded assembly. Only
/// call these methods with interfaces you fully trust. <see cref="LibraryMapper.Emit{TInterface}"/>
/// performs the same generation inside an isolated worker process instead, which contains that risk.
/// </remarks>
public static class EmitDllMappableClass
{
    // Cache keyed by (interface type, calling convention) — the generated delegate attributes differ
    // per convention. Values are Lazy<Type> (not a bare Type) so the compile+load step underneath
    // (CompileMappingType) runs at most ONCE per key even under concurrent first calls: unlike a plain
    // ConcurrentDictionary<TKey, Type>.GetOrAdd, letting the factory itself run twice concurrently is
    // NOT just wasted work here — AssemblyLoadContext.Default.LoadFromStream throws FileLoadException
    // when two distinct compiled assemblies sharing the same simple name (derived from the interface
    // name) are loaded concurrently. ExecutionAndPublication makes every caller for the same key block
    // on the single winning Lazy<Type>, so only one compilation/load ever happens per key.
    //
    // Known limitation: entries are never evicted. This is fine for the intended usage (a small,
    // fixed set of native interfaces known ahead of time), but a caller that repeatedly calls Emit
    // with many distinct dynamically-generated interface types over the lifetime of a long-running
    // process would grow this cache (and the compiled assemblies it keeps alive) without bound.
    private static readonly ConcurrentDictionary<(Type, CallingConvention), Lazy<Type>> emittedLibraries = new();

    /// <summary>
    /// Emits a class that implements the specified interface and maps the interface methods to DLL functions.
    /// Each delegate gets an <see cref="UnmanagedFunctionPointerAttribute"/> carrying the requested convention.
    /// </summary>
    /// <typeparam name="TInterface">Interface type</typeparam>
    /// <param name="callingConvention">The calling convention of the unmanaged functions.</param>
    /// <returns>An instance of the dynamically generated class</returns>
    [Experimental(LibraryMapper.CodeGenerationExperimentalDiagnosticId)]
    public static TInterface Emit<TInterface>(CallingConvention callingConvention) where TInterface : class
        => (TInterface)Emit(typeof(TInterface), callingConvention);

    /// <summary>
    /// Emits a class that implements the specified interface and maps the interface methods to DLL functions.
    /// </summary>
    /// <param name="type">Interface type</param>
    /// <param name="callingConvention">The calling convention of the unmanaged functions.</param>
    /// <returns>An instance of the dynamically generated class</returns>
    [Experimental(LibraryMapper.CodeGenerationExperimentalDiagnosticId)]
    public static object Emit(Type type, CallingConvention callingConvention = CallingConvention.Winapi)
    {
        if (!type.IsInterface)
            throw new NotSupportedException($"{type.Name} is not an interface. Only interfaces are supported.");

        Lazy<Type> lazyEmittedType = emittedLibraries.GetOrAdd(
            (type, callingConvention),
            static key => new Lazy<Type>(
                () => CompileMappingType(key.Item1, key.Item2),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return Activator.CreateInstance(lazyEmittedType.Value);
    }

    /// <summary>
    /// Generates, compiles and resolves the mapping type for <paramref name="type"/>. Guaranteed to
    /// run at most once per (interface, calling convention) pair — see the <c>Lazy&lt;Type&gt;</c>
    /// remarks on <see cref="emittedLibraries"/> for why that guarantee matters here.
    /// </summary>
    /// <param name="type">Interface type to generate a mapping class for.</param>
    /// <param name="callingConvention">The calling convention of the unmanaged functions.</param>
    /// <returns>The compiled, loaded mapping <see cref="Type"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when compilation succeeds but the expected generated type cannot be found in the
    /// resulting assembly, so a broken cache entry is never stored.
    /// </exception>
    private static Type CompileMappingType(Type type, CallingConvention callingConvention)
    {
        string className = type.Name.StartsWith("I", StringComparison.InvariantCultureIgnoreCase)
            ? type.Name.Substring(1)
            : "C" + type.Name;

        StringBuilder csCode = new StringBuilder();
        csCode.AppendLine("namespace DllMapperClasses {");
        csCode.AppendLine("[System.Runtime.CompilerServices.CompilerGenerated]");
        csCode.AppendLine($"\tpublic class {className} : {typeof(LibraryMapper).FullName}, {type.FullName}");
        csCode.AppendLine("\t{");

        foreach (var method in type.GetMethods())
        {
            string delegateClassName = csCode.WriteDelegateClass(method, callingConvention);
            string delegateFieldName = csCode.WriteDelegateField(method, delegateClassName);
            csCode.WriteFunctionCall(method, delegateFieldName);
        }

        csCode.AppendLine("\t}");
        csCode.AppendLine("}");

        Assembly assembly = Compile(type, csCode);
        Type emittedType = assembly.GetTypes().FirstOrDefault(t => t.Name == className);

        if (emittedType is null)
        {
            throw new InvalidOperationException(
                $"Compilation of the mapping class for '{type.FullName}' succeeded, but the expected " +
                $"generated type '{className}' could not be found in the resulting assembly.");
        }

        return emittedType;
    }

    /// <summary>
    /// Generates a delegate class for the specified method.
    /// </summary>
    /// <param name="csCode">StringBuilder to append generated code</param>
    /// <param name="methodInfo">MethodInfo of the interface method</param>
    /// <returns>The name of the generated delegate class</returns>
    private static string WriteDelegateClass(this StringBuilder csCode, MethodInfo methodInfo, CallingConvention callingConvention)
    {
        string delegateName = methodInfo.Name + "Delegate";
        // Emit [UnmanagedFunctionPointer] so the P/Invoke marshaller uses the correct calling convention.
        csCode.AppendLine($"\t\t[{typeof(UnmanagedFunctionPointerAttribute).FullName}({typeof(CallingConvention).FullName}.{callingConvention})]");
        csCode.Append($"\t\tprivate delegate {methodInfo.ReturnType.FullName} {delegateName} ");
        csCode.WriteFunctionParameters(methodInfo, true);
        csCode.AppendLine(";");

        return delegateName;
    }

    /// <summary>
    /// Writes the function parameters for a method to the provided StringBuilder.
    /// </summary>
    /// <param name="csCode">StringBuilder to append generated code</param>
    /// <param name="methodInfo">MethodInfo of the interface method</param>
    /// <param name="declaration">Indicates if this is for declaration (true) or invocation (false)</param>
    private static void WriteFunctionParameters(this StringBuilder csCode, MethodInfo methodInfo, bool declaration)
    {
        csCode.Append(" ( ");
        foreach (var parameter in methodInfo.GetParameters())
        {
            if (parameter.Position > 0) csCode.Append(", ");

            bool isByRefParameter = parameter.ParameterType.IsByRef;

            // Valid C# parameter syntax is [attributes] modifier type name — attributes must come
            // before the ref/out modifier, not after it.
            //
            // For byref (ref/out keyword) parameters, skip re-emitting [In]/[Out]: reflection
            // cannot distinguish "the compiler set the Out flag because of the 'out' keyword" from
            // "the source had an explicit [Out] attribute" — ParameterInfo.GetCustomAttribute<OutAttribute>()
            // returns non-null in both cases — so re-emitting it here would wrongly turn every plain
            // 'out' parameter into invalid "out [OutAttribute] ..." syntax. Non-byref parameters (for
            // example "[Out] StringBuilder buffer", a common P/Invoke idiom) have no such ambiguity:
            // there is no keyword conveying direction, so the attribute is the only way to express it
            // and is preserved.
            if (declaration)
            {
                if (!isByRefParameter && parameter.GetCustomAttribute<InAttribute>() is not null)
                    csCode.Append($"[{typeof(InAttribute).FullName}]");
                if (!isByRefParameter && parameter.GetCustomAttribute<OutAttribute>() is not null)
                    csCode.Append($"[{typeof(OutAttribute).FullName}]");
            }

            // Append ref/out for by-ref parameters
            if (isByRefParameter)
            {
                csCode.Append(parameter.IsOut ? "out " : "ref ");
            }

            // For declarations, include the parameter type
            if (declaration)
            {
                csCode.Append(parameter.ParameterType.FullName.TrimEnd('&')).Append(" ");
            }

            csCode.Append(parameter.Name); // Always append the parameter name
        }
        csCode.Append(" )");
    }

    /// <summary>
    /// Generates a delegate field for the specified method.
    /// </summary>
    /// <param name="csCode">StringBuilder to append generated code</param>
    /// <param name="methodInfo">MethodInfo of the interface method</param>
    /// <param name="delegateClassName">The name of the delegate class</param>
    /// <returns>The name of the generated delegate field</returns>
    private static string WriteDelegateField(this StringBuilder csCode, MethodInfo methodInfo, string delegateClassName)
    {
        var externalAttribute = methodInfo.GetCustomAttribute<ExternalAttribute>(true);
        string externalAttributeName = externalAttribute?.Name ?? methodInfo.Name;
        csCode.AppendLine($"\t\t[{typeof(ExternalAttribute).FullName}(\"{externalAttributeName}\")]");

        string delegateFieldName = "__" + methodInfo.Name;
        csCode.AppendLine($"\t\tprivate {delegateClassName} {delegateFieldName};");
        return delegateFieldName;
    }

    /// <summary>
    /// Generates the function call implementation for the interface method.
    /// </summary>
    /// <param name="csCode">StringBuilder to append generated code</param>
    /// <param name="methodInfo">MethodInfo of the interface method</param>
    /// <param name="delegateFieldName">The name of the delegate field</param>
    private static void WriteFunctionCall(this StringBuilder csCode, MethodInfo methodInfo, string delegateFieldName)
    {
        csCode.Append("public ").Append(methodInfo.ReturnType.FullName).Append(" ").Append(methodInfo.Name);
        csCode.WriteFunctionParameters(methodInfo, true);
        csCode.Append(" => ").Append(delegateFieldName);
        csCode.WriteFunctionParameters(methodInfo, false);
        csCode.AppendLine(";");
    }

    /// <summary>
    /// Compiles the generated C# source code into an assembly.
    /// </summary>
    /// <param name="type">The interface type</param>
    /// <param name="csCode">Generated C# source code</param>
    /// <returns>The compiled assembly</returns>
    private static Assembly Compile(Type type, StringBuilder csCode)
    {
        var compiledCode = GenerateCode(csCode.ToString(), type);
        using MemoryStream output = new MemoryStream();
        var result = compiledCode.Emit(output);

        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
        }

        output.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(output);
    }

    /// <summary>
    /// Generates a CSharpCompilation object from the source code.
    /// </summary>
    /// <param name="sourceCode">Source code string</param>
    /// <param name="type">The interface type</param>
    /// <returns>A CSharpCompilation object</returns>
    private static CSharpCompilation GenerateCode(string sourceCode, Type type)
    {
        var codeString = SourceText.From(sourceCode);
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp7_3);
        var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

        return CSharpCompilation.Create(type.Name + ".dll",
            new[] { parsedSyntaxTree },
                references: BuildReferences(type),
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default
                )
            );
    }

    /// <summary>
    /// Builds the metadata references needed to compile the generated mapping class.
    /// </summary>
    /// <remarks>
    /// Prefers the host's <c>TRUSTED_PLATFORM_ASSEMBLIES</c> list (populated by the .NET runtime
    /// for ordinary framework-dependent and self-contained deployments) over hardcoded
    /// <see cref="Assembly.Load(string)"/> calls by short assembly name, which can fail on trimmed,
    /// AOT, or otherwise non-standard hosts where <c>System.Runtime</c>/<c>netstandard</c> facades
    /// are not guaranteed to be loadable that way. Falls back to the previous behavior when that
    /// data isn't populated (uncommon custom hosts).
    /// </remarks>
    /// <param name="type">The interface type being mapped, whose declaring assembly must be referenced.</param>
    /// <returns>The metadata references to compile against.</returns>
    private static List<MetadataReference> BuildReferences(Type type)
    {
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();

        void AddReference(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path) && addedPaths.Add(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies)
        {
            foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                AddReference(path);
            }
        }
        else
        {
            // Fallback for hosts that don't populate TRUSTED_PLATFORM_ASSEMBLIES.
            AddReference(typeof(object).Assembly.Location);
            AddReference(Assembly.Load("System.Runtime").Location);
            AddReference(Assembly.Load("netstandard").Location);
        }

        AddReference(type.Assembly.Location);
        AddReference(typeof(LibraryMapper).Assembly.Location);

        return references;
    }
}
