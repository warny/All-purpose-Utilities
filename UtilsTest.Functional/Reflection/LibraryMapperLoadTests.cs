using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates native-library loading and member validation for <see cref="LibraryMapper"/>.
/// </summary>
[TestClass]
public class LibraryMapperLoadTests
{
    private class MissingExportMapper : LibraryMapper
    {
        [External("DoesNotExist_XYZ_12345")]
        public Action? FakeFunction;
    }

    [TestMethod]
    public void Create_WhenExportMissing_ThrowsAndFreesHandle()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Test uses kernel32.dll; skipped on non-Windows.");
            return;
        }

        Assert.ThrowsExactly<EntryPointNotFoundException>(
            () => LibraryMapper.Create<MissingExportMapper>("kernel32.dll"),
            "Create must propagate EntryPointNotFoundException when an export is missing.");
    }

    private class ReadOnlyPropertyMapper : LibraryMapper
    {
        private Action? _fn;

        [External("GetCurrentProcessId")]
        public Action? Fn => _fn; // read-only: no setter
    }

    [TestMethod]
    public void Create_WhenPropertyHasNoSetter_ThrowsBeforeCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Test uses kernel32.dll; skipped on non-Windows.");
            return;
        }

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => LibraryMapper.Create<ReadOnlyPropertyMapper>("kernel32.dll"));

        StringAssert.Contains(ex.Message, "no setter",
            "Error message must indicate that the property has no setter.");
    }

    private class CustomSetterMapper : LibraryMapper
    {
        private Action? _fn;

        // Custom setter body — not an auto-property: must be rejected.
        [External("GetCurrentProcessId")]
        public Action? NativeFunction
        {
            get => _fn;
            set => _fn = value;
        }
    }

    [TestMethod]
    public void Create_WhenPropertyHasCustomSetterBody_ThrowsInvalidOperationException()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Test uses kernel32.dll; skipped on non-Windows.");
            return;
        }

        // A non-auto-property setter cannot be guaranteed to accept null unconditionally,
        // so Create must reject it in the prepare phase before loading any export.
        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => LibraryMapper.Create<CustomSetterMapper>("kernel32.dll"));

        StringAssert.Contains(ex.Message, "custom setter",
            "Error message must explain that custom setter bodies are not allowed.");
    }
}
