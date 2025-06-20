using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net;

namespace UtilsTest.Packaging
{
    /// <summary>
    /// Tests ensuring that NuGet package metadata is present in the compiled assemblies.
    /// </summary>
    [TestClass]
    public class MetadataTests
    {
        /// <summary>
        /// Verifies that the <see cref="AssemblyCompanyAttribute"/> is correctly defined.
        /// </summary>
        [TestMethod]
        public void CompanyAttributeIsSet()
        {
            var assembly = typeof(IpRange).Assembly;
            var attribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            Assert.IsNotNull(attribute);
            Assert.AreEqual("Olivier MARTY", attribute.Company);
        }
    }
}
