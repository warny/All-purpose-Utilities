using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using Utils.XML;

namespace UtilsTest.Security.Xml;

/// <summary>
/// Verifies that the unsafe (non-DTD-hardened) <see cref="XmlDataProcessor.Read(string)"/> API
/// carries a warning steering callers toward <see cref="XmlDataProcessor.ReadSecure(string)"/>.
/// </summary>
[TestClass]
public class XmlDataProcessorSecurityTests
{
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
