using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Interface-shape coverage for <see cref="IAssemblyCompiler"/>. There is no production implementation
/// yet, so this is a form/API test only: it confirms the interface can be implemented and used as
/// intended via a hand-rolled fake, not that any particular production implementation behaves this way.
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
