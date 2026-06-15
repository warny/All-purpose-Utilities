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

    [TestMethod]
    public void GetXPath_Element_ReturnsCorrectPath()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><parent><child/></parent></root>");
        XmlElement child = (XmlElement)doc.SelectSingleNode("/root/parent/child")!;
        Assert.AreEqual("/root/parent/child", child.GetXPath());
    }

    [TestMethod]
    public void GetXPath_RepeatedSiblings_AddsIndex()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root><item/><item/></root>");
        XmlElement second = (XmlElement)doc.SelectSingleNode("/root/item[2]")!;
        Assert.AreEqual("/root/item[2]", second.GetXPath());
    }

    [TestMethod]
    public void GetXPath_Attribute_ReturnsAtNotation()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root id=\"42\"/>");
        XmlAttribute attr = doc.DocumentElement!.Attributes["id"]!;
        Assert.AreEqual("/root/@id", attr.GetXPath());
    }

    [TestMethod]
    public void GetXPath_Document_ReturnsSlash()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<root/>");
        Assert.AreEqual("/", doc.GetXPath());
    }
}
