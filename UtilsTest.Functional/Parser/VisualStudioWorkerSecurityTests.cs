using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.VisualStudio;
using Utils.Parser.VisualStudio.Worker;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for the security components added to the plugin worker pipeline:
/// Authenticode filtering, descriptor file size guard, and path-containment guard.
/// </summary>
[TestClass]
public class VisualStudioWorkerSecurityTests
{
    // ── PluginAssemblyVerifier ────────────────────────────────────────────────

    [TestMethod]
    public void PluginAssemblyVerifier_Filter_ExcludesUnsignedDllWithoutMarker()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // filter is a no-op on non-Windows
        }

        string dir = CreateTempDir();
        try
        {
            string dll = WriteTempDll(dir, "unsigned.dll");

            string[] result = PluginAssemblyVerifier.Filter([dll]);

            Assert.AreEqual(0, result.Length, "Unsigned DLL without marker must be excluded.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void PluginAssemblyVerifier_Filter_AllowsUnsignedDllWithMarkerFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string dir = CreateTempDir();
        try
        {
            string dll = WriteTempDll(dir, "plugin.dll");
            File.WriteAllText(dll + PluginAssemblyVerifier.AllowUnsignedSuffix, string.Empty);

            string[] result = PluginAssemblyVerifier.Filter([dll]);

            Assert.AreEqual(1, result.Length, "DLL with .allow-unsigned marker must be allowed.");
            Assert.AreEqual(dll, result[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void PluginAssemblyVerifier_Filter_ReBlocksDllWhenMarkerIsDeleted()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string dir = CreateTempDir();
        try
        {
            string dll = WriteTempDll(dir, "reblocked.dll");
            string marker = dll + PluginAssemblyVerifier.AllowUnsignedSuffix;

            // First pass: marker present → allowed.
            File.WriteAllText(marker, string.Empty);
            string[] withMarker = PluginAssemblyVerifier.Filter([dll]);
            Assert.AreEqual(1, withMarker.Length, "DLL must be allowed when marker is present.");

            // Second pass: marker deleted → must be blocked (cache miss because marker timestamp changed).
            File.Delete(marker);
            string[] withoutMarker = PluginAssemblyVerifier.Filter([dll]);
            Assert.AreEqual(0, withoutMarker.Length, "DLL must be blocked after marker is deleted.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void PluginAssemblyVerifier_Filter_EmptyInputReturnsEmpty()
    {
        string[] result = PluginAssemblyVerifier.Filter([]);
        Assert.AreEqual(0, result.Length);
    }

    // ── SyntaxColorizationDescriptorFileParser ────────────────────────────────

    [TestMethod]
    public void DescriptorFileParser_ParseFile_ThrowsWhenFileSizeExceedsLimit()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"oversized-{Guid.NewGuid():N}.syntaxcolor");
        try
        {
            // Write a file just over the 1 MB limit.
            using (var fs = File.OpenWrite(filePath))
            {
                fs.SetLength(SyntaxColorizationDescriptorFileParser.MaxFileSizeBytes + 1);
            }

            var parser = new SyntaxColorizationDescriptorFileParser();
            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                () => parser.ParseFile(filePath));

            StringAssert.Contains(ex.Message, "exceeds");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a temporary directory with a unique name and returns its path.</summary>
    private static string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), $"PluginVerifierTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Writes a minimal dummy file at <paramref name="dir"/>/<paramref name="name"/>
    /// and returns its full path.  The content is not a real PE; that is intentional —
    /// <c>WinVerifyTrust</c> will return a non-zero error for it, exercising the
    /// "no valid Authenticode signature" path.
    /// </summary>
    private static string WriteTempDll(string dir, string name)
    {
        string path = Path.Combine(dir, name);
        File.WriteAllBytes(path, [0x4D, 0x5A]); // "MZ" DOS header stub — not a valid PE
        return path;
    }
}
