using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Packaging
{
    /// <summary>
    /// Tests ensuring that library projects reference a README file for NuGet packaging.
    /// </summary>
    [TestClass]
    public class ReadmeTests
    {
        private static readonly string[] Projects =
        {
            "..\\..\\..\\..\\Utils\\Utils.csproj",
            "..\\..\\..\\..\\Utils.IO\\Utils.IO.csproj",
            "..\\..\\..\\..\\System.Net\\Utils.Net.csproj",
            "..\\..\\..\\..\\Utils.Data\\Utils.Data.csproj",
            "..\\..\\..\\..\\Utils.Fonts\\Utils.Fonts.csproj",
            "..\\..\\..\\..\\Utils.Geography\\Utils.Geography.csproj",
            "..\\..\\..\\..\\Utils.Imaging\\Utils.Imaging.csproj",
            "..\\..\\..\\..\\Utils.Mathematics\\Utils.Mathematics.csproj",
            "..\\..\\..\\..\\Utils.Reflection\\Utils.Reflection.csproj",
            "..\\..\\..\\..\\Utils.VirtualMachine\\Utils.VirtualMachine.csproj"
        };

        /// <summary>
        /// Verifies that each project defines a <c>PackageReadmeFile</c> and that the referenced file exists.
        /// </summary>
        [TestMethod]
        public void AllProjectsHavePackageReadme()
        {
            foreach (var relative in Projects)
            {
                var csprojPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative);
                var document = XDocument.Load(csprojPath);
                var readme = document.Descendants("PackageReadmeFile").FirstOrDefault()?.Value;
                Assert.IsFalse(string.IsNullOrEmpty(readme), $"{csprojPath} is missing PackageReadmeFile");
                var readmePath = Path.Combine(Path.GetDirectoryName(csprojPath)!, readme);
                Assert.IsTrue(File.Exists(readmePath), $"Readme file '{readmePath}' not found");
            }
        }
    }
}
