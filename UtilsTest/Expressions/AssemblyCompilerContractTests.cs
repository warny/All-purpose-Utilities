using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Contract coverage for <see cref="IAssemblyCompiler"/>. There is no production implementation yet, so
/// these tests exercise the interface shape via a hand-rolled fake that fabricates an assembly with
/// <see cref="System.Reflection.Emit"/> — the contract only cares that the returned
/// <see cref="Assembly"/> is exploitable and contains the expected types, not that it came from parsed
/// source text.
/// </summary>
[TestClass]
public class AssemblyCompilerContractTests
{
    /// <summary>
    /// Fake <see cref="IAssemblyCompiler"/> that fabricates one public type per comma-separated name in
    /// its <c>content</c> argument instead of actually parsing it.
    /// </summary>
    private sealed class EmittingAssemblyCompiler : IAssemblyCompiler
    {
        public Assembly CompileAssembly(string content)
        {
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName($"AssemblyCompilerContractTests.Dynamic.{Guid.NewGuid():N}"),
                AssemblyBuilderAccess.Run);
            ModuleBuilder module = assembly.DefineDynamicModule("Main");

            foreach (string typeName in content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                TypeBuilder typeBuilder = module.DefineType(typeName, TypeAttributes.Public);
                typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
                typeBuilder.CreateType();
            }

            return assembly;
        }
    }

    /// <summary>The compiled assembly contains exactly the requested types, each constructible.</summary>
    [TestMethod]
    public void CompileAssembly_ReturnsAssemblyContainingExpectedTypes()
    {
        IAssemblyCompiler compiler = new EmittingAssemblyCompiler();

        Assembly assembly = compiler.CompileAssembly("GeneratedA,GeneratedB");

        string[] typeNames = assembly.GetTypes().Select(static type => type.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(new[] { "GeneratedA", "GeneratedB" }, typeNames);

        foreach (Type type in assembly.GetTypes())
        {
            object? instance = Activator.CreateInstance(type);
            Assert.IsNotNull(instance);
        }
    }
}
