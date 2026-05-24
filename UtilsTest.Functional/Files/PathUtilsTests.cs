using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.Files;

namespace UtilsTest.Files
{
    [TestClass]
    public class PathUtilsTests
    {
        [TestMethod]
        [Ignore]
        public void EnumerateFiles()
        {
            var files = PathUtils.EnumerateFiles(@"c:\program files\*\*\*\*.exe").ToArray();
            Assert.IsTrue(files.Any());
        }

    }
}
