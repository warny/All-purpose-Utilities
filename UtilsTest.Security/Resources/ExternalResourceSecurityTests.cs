using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Utils.Resources;

namespace UtilsTest.Security.Resources;

/// <summary>
/// Security boundary tests for <see cref="ExternalResource"/>: path traversal containment,
/// external-file size limits, and rejection of unauthorized custom resource types.
/// </summary>
[TestClass]
public class ExternalResourceSecurityTests
{
    // ------------------------------------------------------------------ helpers

    private static string CreateTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ExternalResourceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string WriteResx(string dir, string name, params (string Key, string Value)[] entries)
    {
        string path = Path.Combine(dir, name + ".resx");
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<root>");
        foreach (var (key, value) in entries)
        {
            sb.AppendLine($"  <data name=\"{key}\" xml:space=\"preserve\">");
            sb.AppendLine($"    <value>{value}</value>");
            sb.AppendLine("  </data>");
        }
        sb.AppendLine("</root>");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    // ------------------------------------------------------------------ #40 path containment

    [TestMethod]
    public void ExternalFileRef_WithPathTraversal_IsSkipped()
    {
        string dir = CreateTempDir();
        try
        {
            // Write a .resx that tries to escape the directory with a traversal.
            string traversalValue = $"..{Path.DirectorySeparatorChar}secret.txt;System.Text.UTF8Encoding";
            string resxPath = Path.Combine(dir, "Res.resx");
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<root>");
            sb.AppendLine("  <data name=\"EscapeAttempt\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\">");
            sb.AppendLine($"    <value>{traversalValue}</value>");
            sb.AppendLine("  </data>");
            sb.AppendLine("</root>");
            File.WriteAllText(resxPath, sb.ToString());

            var resource = new ExternalResource(dir, "Res", CultureInfo.InvariantCulture);

            // The entry must have been silently dropped.
            Assert.AreEqual(0, resource.Count, "Path traversal entry should be silently rejected.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ------------------------------------------------------------------ #43 maxExternalFileBytes validation

    [TestMethod]
    public void Constructor_ThrowsOnZeroMaxExternalFileBytes()
    {
        string dir = CreateTempDir();
        try
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => new ExternalResource(dir, "Res", CultureInfo.InvariantCulture, maxExternalFileBytes: 0));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void Constructor_ThrowsOnNegativeMaxExternalFileBytes()
    {
        string dir = CreateTempDir();
        try
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => new ExternalResource(dir, "Res", CultureInfo.InvariantCulture, maxExternalFileBytes: -1));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void ExternalTextFile_ExceedingSizeLimit_ThrowsInvalidOperation()
    {
        string dir = CreateTempDir();
        try
        {
            // Write a small data file.
            string dataFile = Path.Combine(dir, "big.txt");
            File.WriteAllText(dataFile, "ABCDE", Encoding.UTF8); // 5 bytes

            // Write a .resx referencing it with a 3-byte limit.
            string resxPath = Path.Combine(dir, "Res.resx");
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<root>");
            sb.AppendLine("  <data name=\"BigFile\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\">");
            sb.AppendLine("    <value>big.txt;System.String;utf-8</value>");
            sb.AppendLine("  </data>");
            sb.AppendLine("</root>");
            File.WriteAllText(resxPath, sb.ToString());

            var resource = new ExternalResource(dir, "Res", CultureInfo.InvariantCulture, maxExternalFileBytes: 3);

            // Accessing the value must throw because the file exceeds the 3-byte limit.
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = resource["BigFile"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void ExternalBinaryFile_ExceedingSizeLimit_ThrowsInvalidOperation()
    {
        string dir = CreateTempDir();
        try
        {
            string dataFile = Path.Combine(dir, "data.bin");
            File.WriteAllBytes(dataFile, new byte[] { 1, 2, 3, 4, 5 }); // 5 bytes

            string resxPath = Path.Combine(dir, "Res.resx");
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<root>");
            sb.AppendLine("  <data name=\"BinFile\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\">");
            sb.AppendLine("    <value>data.bin;System.Byte[]</value>");
            sb.AppendLine("  </data>");
            sb.AppendLine("</root>");
            File.WriteAllText(resxPath, sb.ToString());

            var resource = new ExternalResource(dir, "Res", CultureInfo.InvariantCulture, maxExternalFileBytes: 3);

            Assert.ThrowsExactly<InvalidOperationException>(() => _ = resource["BinFile"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ------------------------------------------------------------------ #41 arbitrary type rejection

    [TestMethod]
    public void ExternalFileRef_WithUnknownCustomType_IsSkipped()
    {
        string dir = CreateTempDir();
        try
        {
            string resxPath = Path.Combine(dir, "Res.resx");
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<root>");
            sb.AppendLine("  <data name=\"CustomTypeRef\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\">");
            sb.AppendLine("    <value>file.dat;MyNamespace.ArbitraryType, MyAssembly</value>");
            sb.AppendLine("  </data>");
            sb.AppendLine("</root>");
            File.WriteAllText(resxPath, sb.ToString());

            // Construction must not throw; the custom-type entry is silently rejected.
            var resource = new ExternalResource(dir, "Res", CultureInfo.InvariantCulture);
            Assert.AreEqual(0, resource.Count, "Unknown custom-type entries should be silently rejected.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
