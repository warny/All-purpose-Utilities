using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Interface-shape coverage for <see cref="ITypeCompiler"/>. There is no production implementation yet,
/// so these are form/API tests only: the assignability and constructor-related exceptions asserted here
/// come entirely from <see cref="EmittingTypeCompiler"/>'s own logic, not from any behavior mandated by
/// <see cref="ITypeCompiler"/> itself. They confirm the interface can be implemented and used as
/// intended, but a future production implementation is not required by these tests to throw the same
/// exception types or under the same exact conditions.
/// </summary>
[TestClass]
public class TypeCompilerContractTests
{
    /// <summary>Marker interface a fake-compiled type may or may not implement.</summary>
    public interface IMarker
    {
    }

    /// <summary>
    /// Fake <see cref="ITypeCompiler"/> that fabricates a type per named scenario instead of actually
    /// parsing its <c>content</c> argument.
    /// </summary>
    private sealed class EmittingTypeCompiler : ITypeCompiler
    {
        /// <summary>Fabricates a type per named <paramref name="content"/> scenario via <see cref="System.Reflection.Emit"/>.</summary>
        public Type CompileType(string content)
        {
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName($"TypeCompilerContractTests.Dynamic.{Guid.NewGuid():N}"),
                AssemblyBuilderAccess.Run);
            ModuleBuilder module = assembly.DefineDynamicModule("Main");
            TypeBuilder typeBuilder = module.DefineType($"Generated_{content}", TypeAttributes.Public);

            switch (content)
            {
                case "valid-marker":
                    typeBuilder.AddInterfaceImplementation(typeof(IMarker));
                    typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
                    break;
                case "incompatible":
                    typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
                    break;
                case "no-parameterless-ctor":
                    typeBuilder.AddInterfaceImplementation(typeof(IMarker));
                    ConstructorBuilder ctor = typeBuilder.DefineConstructor(
                        MethodAttributes.Public, CallingConventions.Standard, [typeof(int)]);
                    ILGenerator il = ctor.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
                    il.Emit(OpCodes.Ret);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown fake type-compiler content '{content}'.");
            }

            return typeBuilder.CreateType();
        }

        /// <summary>Compiles the type then builds a cached factory validating assignability and a parameterless constructor.</summary>
        public Func<T> CompileType<T>(string content)
        {
            Type type = CompileType(content);
            if (!typeof(T).IsAssignableFrom(type))
            {
                throw new InvalidOperationException($"Compiled type '{type}' is not assignable to '{typeof(T)}'.");
            }

            ConstructorInfo? ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor is null)
            {
                throw new InvalidOperationException($"Compiled type '{type}' has no accessible parameterless constructor.");
            }

            NewExpression newExpression = Expression.New(ctor);
            Expression body = typeof(T) == type ? newExpression : Expression.Convert(newExpression, typeof(T));
            return Expression.Lambda<Func<T>>(body).Compile();
        }
    }

    /// <summary>Compiling a type returns a <see cref="Type"/> assignable to the expected interface.</summary>
    [TestMethod]
    public void CompileType_ReturnsTypeAssignableToExpectedInterface()
    {
        ITypeCompiler compiler = new EmittingTypeCompiler();

        Type compiled = compiler.CompileType("valid-marker");

        Assert.IsTrue(typeof(IMarker).IsAssignableFrom(compiled));
    }

    /// <summary>The generic overload returns a working factory that constructs new instances.</summary>
    [TestMethod]
    public void CompileType_Generic_ReturnsWorkingFactory()
    {
        ITypeCompiler compiler = new EmittingTypeCompiler();

        Func<IMarker> factory = compiler.CompileType<IMarker>("valid-marker");
        IMarker first = factory();
        IMarker second = factory();

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotSame(first, second);
    }

    /// <summary>A compiled type incompatible with the requested type throws explicitly.</summary>
    [TestMethod]
    public void CompileType_Generic_IncompatibleType_Throws()
    {
        ITypeCompiler compiler = new EmittingTypeCompiler();

        Assert.ThrowsExactly<InvalidOperationException>(() => compiler.CompileType<IMarker>("incompatible"));
    }

    /// <summary>A compiled type without a usable parameterless constructor throws explicitly.</summary>
    [TestMethod]
    public void CompileType_Generic_MissingParameterlessConstructor_Throws()
    {
        ITypeCompiler compiler = new EmittingTypeCompiler();

        Assert.ThrowsExactly<InvalidOperationException>(() => compiler.CompileType<IMarker>("no-parameterless-ctor"));
    }
}
