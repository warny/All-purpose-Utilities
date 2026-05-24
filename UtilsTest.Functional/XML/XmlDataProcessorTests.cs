using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Utils.XML;

namespace UtilsTest.XML;

[TestClass]
public class XmlDataProcessorTests
{
    private sealed class SampleProcessor : XmlDataProcessor
    {
        public List<string> Values { get; } = new List<string>();

        [Match("/root/item")]
        private void HandleItem()
        {
            Values.Add(ValueOf("@id"));
        }

        [Match("/root/item/value")]
        private void HandleValue()
        {
            Values.Add(ValueOf());
        }

        protected override void Root()
        {
            Apply("/root/item");
            Apply("/root/item/value");
        }
    }

    [TestMethod]
    public void ReadProcessesNodesWithMatchingHandlers()
    {
        const string xml = "<root><item id=\"A\"><value>First</value></item><item id=\"B\"><value>Second</value></item></root>";
        SampleProcessor processor = new SampleProcessor();

        using XmlReader reader = XmlReader.Create(new StringReader(xml));
        processor.Read(reader);

        CollectionAssert.AreEquivalent(new[] { "A", "First", "B", "Second" }, processor.Values);
    }

    [TestMethod]
    public void ReadSecureProcessesNodesWithMatchingHandlers()
    {
        const string xml = "<root><item id=\"A\"><value>First</value></item><item id=\"B\"><value>Second</value></item></root>";
        SampleProcessor processor = new SampleProcessor();
        string filePath = Path.GetTempFileName();

        try
        {
            File.WriteAllText(filePath, xml);
            processor.ReadSecure(filePath);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        CollectionAssert.AreEquivalent(new[] { "A", "First", "B", "Second" }, processor.Values);
    }

    [TestMethod]
    public void ReadString_ShouldBeMarkedAsObsoleteWarning()
    {
        MethodInfo? method = typeof(XmlDataProcessor).GetMethod(nameof(XmlDataProcessor.Read), new[] { typeof(string) });
        Assert.IsNotNull(method);

        ObsoleteAttribute? attribute = method.GetCustomAttribute<ObsoleteAttribute>();
        Assert.IsNotNull(attribute);
        Assert.IsFalse(attribute.IsError);
        StringAssert.Contains(attribute.Message, "ReadSecure");
    }
}
