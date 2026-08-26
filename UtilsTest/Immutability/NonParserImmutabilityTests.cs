using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Objects;

namespace UtilsTest.Immutability;

/// <summary>
/// Verifies defensive collection snapshots used by immutable types outside the parser projects.
/// </summary>
[TestClass]
public sealed class NonParserImmutabilityTests
{
    /// <summary>Verifies the existing empty, null, and default Bytes contracts.</summary>
    [TestMethod]
    public void Bytes_EmptyNullAndDefaultRemainEmpty()
    {
        byte[] nullSource = null!;
        Bytes fromNull = nullSource;
        Bytes fromEmpty = System.Array.Empty<byte>();
        Bytes defaultValue = default;

        Assert.AreEqual(0, fromNull.Count);
        Assert.AreEqual(0, fromEmpty.Count);
        Assert.AreEqual(0, defaultValue.Count);
        Assert.AreEqual(0, defaultValue.ToArray().Length);
    }
}
