using System;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Reflection;

/// <summary>
/// Validates the pure argument/permission-building logic extracted from
/// <see cref="EmitWorkerProcess.Start"/>, without spawning a real second process.
/// </summary>
[TestClass]
public class EmitWorkerProcessTests
{
    [TestMethod]
    public void BuildWorkerArguments_SameExecutableAsEntryAssembly_OmitsAssemblyPath()
    {
        string entryAssemblyLocation = Assembly.GetEntryAssembly()!.Location;
        string exePath = System.IO.Path.ChangeExtension(entryAssemblyLocation, ".exe");

        string[] arguments = EmitWorkerProcess.BuildWorkerArguments(exePath, "pipe-name");

        CollectionAssert.AreEqual(
            new[] { "--utils-reflection-emit-worker", "pipe-name" },
            arguments);
    }

    [TestMethod]
    public void BuildWorkerArguments_GenericLauncher_PrependsEntryAssemblyPath()
    {
        string entryAssemblyLocation = Assembly.GetEntryAssembly()!.Location;

        string[] arguments = EmitWorkerProcess.BuildWorkerArguments("/usr/bin/dotnet", "pipe-name");

        CollectionAssert.AreEqual(
            new[] { entryAssemblyLocation, "--utils-reflection-emit-worker", "pipe-name" },
            arguments);
    }

    // ─── Item 7: timeout validation before resource allocation ───────────────────

    [TestMethod]
    public void ValidateTimeout_AcceptsPositiveFiniteDuration()
    {
        TimeSpan result = EmitWorkerProcess.ValidateTimeout(
            TimeSpan.FromSeconds(5), EmitWorkerProcess.DefaultCallTimeout, "test");
        Assert.AreEqual(TimeSpan.FromSeconds(5), result);
    }

    [TestMethod]
    public void ValidateTimeout_UsesDefaultWhenNull()
    {
        TimeSpan result = EmitWorkerProcess.ValidateTimeout(
            null, EmitWorkerProcess.DefaultCallTimeout, "test");
        Assert.AreEqual(EmitWorkerProcess.DefaultCallTimeout, result);
    }

    // ─── Review #495 point 1: assembly-qualified type identity in descriptors ──────

    [TestMethod]
    public void MethodDescriptorDto_StableTypeName_NonByRefType_UsesAssemblyQualifiedName()
    {
        // Ordinary types must use AssemblyQualifiedName so that two types from different assemblies
        // with the same FullName are distinguished during the host-side method matching.
        string name = MethodDescriptorDto.StableTypeName(typeof(int));
        Assert.AreEqual(typeof(int).AssemblyQualifiedName, name);
    }

    [TestMethod]
    public void MethodDescriptorDto_StableTypeName_ByRefInt_EndsWithAmpersand()
    {
        Type byRefInt = typeof(int).MakeByRefType();
        string name = MethodDescriptorDto.StableTypeName(byRefInt);

        Assert.IsTrue(name.EndsWith("&", StringComparison.Ordinal),
            $"By-ref type name must end with '&', got: {name}");
        StringAssert.Contains(name, typeof(int).AssemblyQualifiedName!);
    }

    // ─── Item 15: async lifecycle APIs ───────────────────────────────────────────

    [TestMethod]
    public void EmitWorkerProcess_ImplementsIAsyncDisposable()
    {
        // Verify that the class declares IAsyncDisposable so callers in async contexts
        // can avoid blocking a thread during the Shutdown round-trip.
        Assert.IsTrue(typeof(IAsyncDisposable).IsAssignableFrom(typeof(EmitWorkerProcess)),
            "EmitWorkerProcess must implement IAsyncDisposable (item 15).");
    }

    [TestMethod]
    public void InvokeMethodAsync_MethodExists_ReturnsTaskOfObject()
    {
        MethodInfo? method = typeof(EmitWorkerProcess).GetMethod(
            "InvokeMethodAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method, "InvokeMethodAsync must be declared on EmitWorkerProcess.");
        Assert.IsTrue(
            typeof(System.Threading.Tasks.Task<object?>).IsAssignableFrom(method.ReturnType),
            $"InvokeMethodAsync must return Task<object?>, found {method.ReturnType}.");
    }

    [TestMethod]
    public void LoadInterfaceAsync_MethodExists_ReturnsTaskOfInt()
    {
        MethodInfo? method = typeof(EmitWorkerProcess).GetMethod(
            "LoadInterfaceAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method, "LoadInterfaceAsync must be declared on EmitWorkerProcess.");
        Assert.IsTrue(
            typeof(System.Threading.Tasks.Task<int>).IsAssignableFrom(method.ReturnType),
            $"LoadInterfaceAsync must return Task<int>, found {method.ReturnType}.");
    }

    [TestMethod]
    public void DisposeAsync_MethodExists_ReturnsValueTask()
    {
        MethodInfo? method = typeof(EmitWorkerProcess).GetMethod("DisposeAsync");

        Assert.IsNotNull(method, "DisposeAsync must be declared on EmitWorkerProcess.");
        Assert.AreEqual(typeof(ValueTask), method.ReturnType,
            "DisposeAsync must return ValueTask.");
    }

}
