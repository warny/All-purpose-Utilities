using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Packaging
{
    /// <summary>
    /// Tests related to package versioning.
    /// </summary>
    [TestClass]
    public class VersionTests
    {
        /// <summary>
        /// Ensures that the assembly informational version matches the version specified in the project file.
        /// </summary>
        [TestMethod]
        public void AssemblyVersionMatchesProjectVersion()
        {
            var csproj = Path.Combine("..", "..", "..", "..", "System.Net", "Utils.Net.csproj");
            var doc = XDocument.Load(csproj);
            var version = doc.Descendants("Version").First().Value;
            var assembly = typeof(IpRange).Assembly;
            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            Assert.IsNotNull(info);
            Assert.IsTrue(info.InformationalVersion.StartsWith(version, StringComparison.Ordinal));
        }
    }
}
