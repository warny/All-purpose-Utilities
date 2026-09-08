using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using UtilsTest.NativeInterop;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates architecture-specific native C runtime candidate selection.
/// </summary>
[TestClass]
public class NativeRuntimeLibraryResolverTests
{
    /// <summary>
    /// Verifies that 32-bit ARM Linux probes Alpine armv7 before retaining the legacy armhf fallback.
    /// </summary>
    [TestMethod]
    public void GetLinuxCandidates_Arm_IncludesArmV7AndArmHfMuslNames()
    {
        string[] candidates = NativeRuntimeLibraryResolver.GetLinuxCandidates(Architecture.Arm).ToArray();

        CollectionAssert.AreEqual(
            new[] { "libc.so.6", "libc.musl-armv7.so.1", "libc.musl-armhf.so.1", "libc.so" },
            candidates);
    }

    /// <summary>
    /// Verifies that 32-bit x86 Linux probes Alpine's x86 name before retaining the i386 fallback.
    /// </summary>
    [TestMethod]
    public void GetLinuxCandidates_X86_IncludesX86AndI386MuslNames()
    {
        string[] candidates = NativeRuntimeLibraryResolver.GetLinuxCandidates(Architecture.X86).ToArray();

        CollectionAssert.AreEqual(
            new[] { "libc.so.6", "libc.musl-x86.so.1", "libc.musl-i386.so.1", "libc.so" },
            candidates);
    }
}
