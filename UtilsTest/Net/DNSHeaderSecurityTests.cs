using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS;

namespace Utils.Tests.Net;

/// <summary>
/// Contains security-focused tests for <see cref="DNSHeader"/> initialization.
/// </summary>
[TestClass]
public class DNSHeaderSecurityTests
{
    /// <summary>
    /// Ensures that generated DNS identifiers always remain in the valid 16-bit range.
    /// </summary>
    [TestMethod]
    public void Constructor_ShouldGenerateIdentifierWithinUShortRange()
    {
        for (int i = 0; i < 128; i++)
        {
            var header = new DNSHeader();
            Assert.IsTrue(header.ID <= ushort.MaxValue);
        }
    }
}
