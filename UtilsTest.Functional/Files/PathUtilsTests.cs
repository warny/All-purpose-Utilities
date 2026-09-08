using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using Utils.Files;

namespace UtilsTest.Files
{
    /// <summary>
    /// Validates path wildcard expansion against the real filesystem.
    /// </summary>
    [TestClass]
    public class PathUtilsTests
    {
        /// <summary>
        /// Verifies that wildcard directory segments and file patterns return exactly matching files.
        /// </summary>
        [TestMethod]
        public void EnumerateFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), $"PathUtilsTests-{Guid.NewGuid():N}");
            string matchA = Path.Combine(root, "groupA", "subOne", "match-a.txt");
            string notMatchLog = Path.Combine(root, "groupA", "subTwo", "not-match.log");
            string matchB = Path.Combine(root, "groupB", "subThree", "match-b.txt");
            string outsideMatch = Path.Combine(root, "other", "subFour", "should-not-match.txt");

            try
            {
                foreach (string file in new[] { matchA, notMatchLog, matchB, outsideMatch })
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                    File.WriteAllText(file, file);
                }

                string wildcardPath = Path.Combine(root, "group*", "sub*", "*.txt");
                string[] files = PathUtils.EnumerateFiles(wildcardPath)
                    .Select(Path.GetFullPath)
                    .ToArray();

                CollectionAssert.AreEquivalent(
                    new[] { Path.GetFullPath(matchA), Path.GetFullPath(matchB) },
                    files);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

    }
}
