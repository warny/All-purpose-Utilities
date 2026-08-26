using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Utils.Resources;

namespace UtilsTest.Resources;

/// <summary>
/// Contract tests for <see cref="ExternalResource"/> that do not require real .resx files
/// for dictionary-contract invariants, plus integration tests using temporary .resx files
/// for file-system and value-resolution correctness.
/// </summary>
[TestClass]
public class ExternalResourceContractTests
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

    // ------------------------------------------------------------------ #42 dictionary value consistency

    [TestMethod]
    public void Indexer_TryGetValue_AndValues_ReturnSameLogicalValue()
    {
        string dir = CreateTempDir();
        try
        {
            WriteResx(dir, "Test", ("greeting", "Hello"), ("farewell", "Goodbye"));
            var resource = new ExternalResource(dir, "Test", CultureInfo.InvariantCulture);

            // All three access paths must return identical logical values.
            foreach (var key in resource.Keys)
            {
                object byIndexer = resource[key];
                Assert.IsTrue(resource.TryGetValue(key, out object byTryGet));
                object fromValues = resource.Values.Single(v => v.Equals(byIndexer));

                Assert.AreEqual(byIndexer, byTryGet,
                    $"Key '{key}': indexer and TryGetValue returned different values.");
                Assert.IsNotNull(fromValues,
                    $"Key '{key}': Values enumeration did not contain the resolved value.");
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void Enumeration_ReturnsResolvedValues_NotWrappers()
    {
        string dir = CreateTempDir();
        try
        {
            WriteResx(dir, "Test", ("hello", "world"));
            var resource = new ExternalResource(dir, "Test", CultureInfo.InvariantCulture);

            foreach (var kv in resource)
            {
                // Values must be plain strings, not internal IResXValue wrappers.
                Assert.IsInstanceOfType<string>(kv.Value,
                    $"Key '{kv.Key}': enumeration returned a non-string wrapper object.");
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ------------------------------------------------------------------ basic loading

    [TestMethod]
    public void Load_InlineStringResources()
    {
        string dir = CreateTempDir();
        try
        {
            WriteResx(dir, "Res", ("Key1", "Value1"), ("Key2", "Value2"));
            var resource = new ExternalResource(dir, "Res", CultureInfo.InvariantCulture);

            Assert.AreEqual(2, resource.Count);
            Assert.AreEqual("Value1", resource["Key1"]);
            Assert.AreEqual("Value2", resource["Key2"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void TryGetValue_ReturnsFalse_ForMissingKey()
    {
        string dir = CreateTempDir();
        try
        {
            WriteResx(dir, "Res", ("Key1", "Value1"));
            var resource = new ExternalResource(dir, "Res", CultureInfo.InvariantCulture);

            bool found = resource.TryGetValue("NonExistent", out _);
            Assert.IsFalse(found);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

}
