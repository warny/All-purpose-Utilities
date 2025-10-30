using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.Loader;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Utility class for dynamically generating types that map to DLL functions.
/// This class emits new types at runtime, which implement a specified interface.
/// The methods of the interface are mapped to corresponding unmanaged functions
/// using the DllMapper class.
/// </summary>
public static class EmitDllMappableClass
{
    // Cache to store emitted types to avoid repeated generation for the same interface.
    private static readonly Dictionary<Type, Type> emittedLibraries = new Dictionary<Type, Type>();

    /// <summary>
    /// Emits a class that implements the specified interface and maps the interface methods to DLL functions.
    /// </summary>
    /// <typeparam name="T">Interface type</typeparam>
    /// <param name="callingConvention">The calling convention of the methods</param>
    /// <returns>An instance of the dynamically generated class</returns>
    public static T Emit<T>(CallingConvention callingConvention) where T : class => (T)Emit(typeof(T));

    /// <summary>
    /// Emits a class that implements the specified interface and maps the interface methods to DLL functions.
    /// </summary>
    /// <param name="type">Interface type</param>
    /// <returns>An instance of the dynamically generated class</returns>
    public static object Emit(Type type)
    {
        if (!type.IsInterface)
        {
            throw new NotSupportedException($"{type.Name} is not an interface. Only interfaces are supported.");
        }

        // Check if the type was already generated and cached
        if (!emittedLibraries.TryGetValue(type, out Type emittedType))
        {
            // Generate the class name based on the interface
            string className = type.Name.StartsWith("I", StringComparison.InvariantCultureIgnoreCase)
                ? type.Name.Substring(1) // Remove 'I' prefix
                : "C" + type.Name; // Prefix with 'C' if no 'I'

            // Build the C# source code for the class
            StringBuilder csCode = new StringBuilder();
            csCode.AppendLine("namespace DllMapperClasses {");
            csCode.AppendLine("[System.Runtime.CompilerServices.CompilerGenerated]");
            csCode.AppendLine($"\tpublic class {className} : {typeof(LibraryMapper).FullName}, {type.FullName}");
            csCode.AppendLine("\t{");

            // Generate methods and delegate fields for each interface method
            foreach (var method in type.GetMethods())
            {
                string delegateClassName = csCode.WriteDelegateClass(method);
                string delegateFieldName = csCode.WriteDelegateField(method, delegateClassName);
                csCode.WriteFunctionCall(method, delegateFieldName);
            }

            csCode.AppendLine("\t}");
            csCode.AppendLine("}");

            // Compile the generated code into an assembly
            Assembly assembly = Compile(type, csCode);
            emittedType = assembly.GetTypes().FirstOrDefault(t => t.Name == className);
            emittedLibraries.Add(type, emittedType); // Cache the generated type
        }

        return Activator.CreateInstance(emittedType); // Create an instance of the generated type
    }

    /// <summary>
    /// Generates a delegate class for the specified method.
    /// </summary>
    /// <param name="csCode">StringBuilder to append generated code</param>
    /// <param name="methodInfo">MethodInfo of the interface method</param>
    /// <returns>The name of the generated delegate class</returns>
    private static string WriteDelegateClass(this StringBuilder csCode, MethodInfo methodInfo)
    {
        string delegateName = methodInfo.Name + "Delegate";
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

            // Append ref/out for by-ref parameters
            if (parameter.ParameterType.IsByRef)
            {
                csCode.Append(parameter.IsOut ? "out " : "ref ");
            }

            // For declarations, include the parameter type
            if (declaration)
            {
                if (parameter.GetCustomAttribute<InAttribute>() is not null) csCode.Append($"[{typeof(InAttribute).FullName}]");
                if (parameter.GetCustomAttribute<OutAttribute>() is not null) csCode.Append($"[{typeof(OutAttribute).FullName}]");
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

        // Get references to required assemblies
        Assembly systemRuntime = Assembly.Load("System.Runtime");
        Assembly netStandard = Assembly.Load("netstandard");

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(type.Assembly.Location),
            MetadataReference.CreateFromFile(typeof(LibraryMapper).Assembly.Location),
            MetadataReference.CreateFromFile(systemRuntime.Location),
            MetadataReference.CreateFromFile(netStandard.Location)
        };

        return CSharpCompilation.Create(type.Name + ".dll",
            new[] { parsedSyntaxTree },
                references: references,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default
                )
            );
    }
}
