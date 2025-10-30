using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Utils.Resources;

namespace UtilsTest.Resources;

[TestClass]
public class ResourceReaderTest
{
    string baseDirectory = Path.Combine(AppContext.BaseDirectory, "Resources");

    [TestMethod]
    public void ReadResourceFile()
    {
        ExternalResource resource = new ExternalResource(baseDirectory, "TestResource");
        Assert.AreNotEqual(0, resource.Count);
    }

    [TestMethod]
    public void TestStringResource()
    {
        ExternalResource resource = new ExternalResource(baseDirectory, "TestResource");
        Assert.AreEqual("ValeurTest1", resource["StringTest1"]);
        Assert.AreEqual("ValeurTest2", resource["StringTest2"]);
        Assert.AreEqual("Ceci est un test de texte", resource["TextFile1"]);
    }

    [TestMethod]
    public void TestLocalizedResource()
    {
        ExternalResource resourceEn = new ExternalResource(baseDirectory, "TestResource", CultureInfo.GetCultureInfo("en-US"));
        Assert.AreEqual("TestLocaleInt", resourceEn["StringLocale"]);
        ExternalResource resourceFr = new ExternalResource(baseDirectory, "TestResource", CultureInfo.GetCultureInfo("fr-FR"));
        Assert.AreEqual("TestLocaleFR", resourceFr["StringLocale"]);
    }
}
