using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Utils.XML;

namespace UtilsTest.XML;

[TestClass]
public class XmlUtilsTests
{
    [TestMethod]
    public void ReadChildElementsEnumeratesImmediateChildren()
    {
        using XmlReader reader = XmlReader.Create(new StringReader("<root><child>A</child><child>B</child></root>"));
        reader.ReadToFollowing("root");

        List<string> children = new List<string>();
        foreach (XmlReader child in reader.ReadChildElements())
        {
            children.Add(child.Name);
        }

        CollectionAssert.AreEqual(new[] { "child", "child" }, children);
    }

    [TestMethod]
    public void ReadChildElementsWithNameFiltersChildren()
    {
        using XmlReader reader = XmlReader.Create(new StringReader("<root><child>A</child><other>skip</other><child>B</child></root>"));
        reader.ReadToFollowing("root");

        List<string> children = new List<string>();
        foreach (XmlReader child in reader.ReadChildElements("child"))
        {
            children.Add(child.Name);
        }

        CollectionAssert.AreEqual(new[] { "child", "child" }, children);
    }
}
