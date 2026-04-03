using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Inspects managed assembly IL metadata for dangerous API references
/// before the assembly is loaded into the current process.
/// </summary>
/// <remarks>
/// <para>
/// The scanner operates entirely on the on-disk PE file without executing any code.
/// It detects three categories of violation:
/// <list type="bullet">
///   <item>External type references that belong to blocked namespaces or are blocked types.</item>
///   <item>External method references that call specifically blocked methods on semi-safe types.</item>
///   <item>P/Invoke declarations (native code interop).</item>
/// </list>
/// </para>
/// <para>
/// Read-only file access (<c>File.ReadAllText</c>, <c>File.OpenRead</c>, etc.) is permitted
/// intentionally, as it will be needed for future autocomplete support.
/// </para>
/// <para>
/// <strong>Known limitation:</strong> reflection-based invocation (e.g.
/// <c>Type.GetType("System.IO.File").GetMethod("Delete").Invoke(...)</c>) and
/// dynamic code generation cannot be detected by static IL analysis.
/// Full isolation requires running colorization profiles in a separate OS-level
/// sandboxed process, which is outside the scope of this implementation.
/// </para>
/// </remarks>
internal static class AssemblySecurityInspector
{
    /// <summary>
    /// Namespace prefixes whose presence in external type references is blocked entirely.
    /// </summary>
    private static readonly string[] BlockedNamespacePrefixes =
    [
        "System.Net",       // HTTP, WebClient, sockets, DNS — all network access
        "Microsoft.Win32",  // Registry, shell operations
    ];

    /// <summary>
    /// Fully-qualified type names that are blocked at the type-reference level.
    /// Any assembly that imports one of these types is rejected.
    /// </summary>
    private static readonly HashSet<string> BlockedTypeNames = new(StringComparer.Ordinal)
    {
        // File types that have no legitimate read-only usage path:
        "System.IO.FileStream",             // constructor accepts write modes; use StreamReader instead
        "System.IO.StreamWriter",           // write-only
        "System.IO.BinaryWriter",           // write-only

        // Process spawning:
        "System.Diagnostics.Process",
        "System.Diagnostics.ProcessStartInfo",

        // Dynamic code / cross-domain loading:
        "System.AppDomain",
        "System.Runtime.Loader.AssemblyLoadContext",
    };

    /// <summary>
    /// Per-type allowlist of blocked method names.
    /// A method call is rejected when the declaring type and method name both appear here.
    /// Types listed here are allowed as a whole but restrict specific dangerous methods.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> BlockedMethodsByType =
        new(StringComparer.Ordinal)
        {
            ["System.IO.File"] =
            [
                // Write / delete / move operations — read operations are permitted.
                "Delete", "Create", "CreateText", "OpenWrite",
                "WriteAllText", "WriteAllBytes", "WriteAllLines",
                "AppendAllText", "AppendAllLines", "AppendText",
                "Move", "Copy", "Replace",
                "Encrypt", "Decrypt",
                "SetAttributes",
                "SetCreationTime", "SetCreationTimeUtc",
                "SetLastAccessTime", "SetLastAccessTimeUtc",
                "SetLastWriteTime", "SetLastWriteTimeUtc",
            ],
            ["System.IO.Directory"] =
            [
                "CreateDirectory", "CreateTempSubdirectory",
                "Delete", "Move",
                "SetCurrentDirectory",
            ],
            ["System.Environment"] =
            [
                "Exit", "FailFast",
            ],
            ["System.Reflection.Assembly"] =
            [
                // Dynamic loading of arbitrary assemblies is blocked.
                "Load", "LoadFrom", "LoadFile",
                "LoadWithPartialName",
                "ReflectionOnlyLoad", "ReflectionOnlyLoadFrom",
                "UnsafeLoadFrom",
            ],
        };

    /// <summary>
    /// Inspects an assembly file's IL metadata for dangerous API references without loading it.
    /// </summary>
    /// <param name="assemblyFilePath">Path to the assembly DLL on disk.</param>
    /// <param name="violations">
    /// Human-readable descriptions of detected violations.
    /// Empty when the method returns <see langword="true"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when no dangerous patterns are detected and the assembly may be loaded;
    /// <see langword="false"/> when the assembly must be rejected.
    /// </returns>
    public static bool IsSafe(string assemblyFilePath, out IReadOnlyList<string> violations)
    {
        var found = new List<string>();
        violations = found;

        try
        {
            using var peStream = new FileStream(assemblyFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var peReader = new PEReader(peStream);

            if (!peReader.HasMetadata)
            {
                // Pure native binary — no managed IL to inspect.
                return true;
            }

            MetadataReader metadata = peReader.GetMetadataReader();

            ScanTypeReferences(metadata, found);
            ScanMemberReferences(metadata, found);
            ScanPInvokeMethods(metadata, found);
        }
        catch (Exception ex)
        {
            found.Add($"Assembly could not be inspected: {ex.Message}");
        }

        return found.Count == 0;
    }

    /// <summary>
    /// Scans external type references for blocked namespaces and type names.
    /// </summary>
    private static void ScanTypeReferences(MetadataReader metadata, List<string> violations)
    {
        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            TypeReference typeRef = metadata.GetTypeReference(handle);
            string ns = metadata.GetString(typeRef.Namespace);
            string name = metadata.GetString(typeRef.Name);
            string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

            if (IsBlockedType(ns, fullName))
            {
                violations.Add($"Blocked type reference: {fullName}");
            }
        }
    }

    /// <summary>
    /// Scans external member references for blocked methods on semi-safe types
    /// (types allowed overall but with specific dangerous methods).
    /// </summary>
    private static void ScanMemberReferences(MetadataReader metadata, List<string> violations)
    {
        foreach (MemberReferenceHandle handle in metadata.MemberReferences)
        {
            MemberReference memberRef = metadata.GetMemberReference(handle);

            if (memberRef.GetKind() != MemberReferenceKind.Method)
            {
                continue;
            }

            if (memberRef.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var parentHandle = (TypeReferenceHandle)memberRef.Parent;
            TypeReference parentType = metadata.GetTypeReference(parentHandle);
            string parentNs = metadata.GetString(parentType.Namespace);
            string parentName = metadata.GetString(parentType.Name);
            string parentFullName = string.IsNullOrEmpty(parentNs) ? parentName : $"{parentNs}.{parentName}";

            if (!BlockedMethodsByType.TryGetValue(parentFullName, out HashSet<string>? blockedMethods))
            {
                continue;
            }

            string methodName = metadata.GetString(memberRef.Name);
            if (blockedMethods.Contains(methodName))
            {
                violations.Add($"Blocked method call: {parentFullName}.{methodName}");
            }
        }
    }

    /// <summary>
    /// Scans method definitions for P/Invoke (native code interop) declarations
    /// and reports the native DLL name alongside the managed method name.
    /// </summary>
    private static void ScanPInvokeMethods(MetadataReader metadata, List<string> violations)
    {
        foreach (MethodDefinitionHandle handle in metadata.MethodDefinitions)
        {
            MethodDefinition method = metadata.GetMethodDefinition(handle);
            if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0)
            {
                continue;
            }

            string methodName = metadata.GetString(method.Name);
            MethodImport import = method.GetImport();
            string moduleName = import.Module.IsNil
                ? "<unknown>"
                : metadata.GetString(metadata.GetModuleReference(import.Module).Name);

            violations.Add($"P/Invoke (native interop) '{methodName}' → {moduleName}");
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when a type matches the blocked namespace prefixes or type names.
    /// </summary>
    private static bool IsBlockedType(string ns, string fullName)
    {
        if (BlockedTypeNames.Contains(fullName))
        {
            return true;
        }

        foreach (string prefix in BlockedNamespacePrefixes)
        {
            if (ns.Equals(prefix, StringComparison.Ordinal) ||
                ns.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
