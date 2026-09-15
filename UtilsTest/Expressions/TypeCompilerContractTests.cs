using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Contract coverage for <see cref="ITypeCompiler"/>. There is no production implementation yet, so
/// these tests exercise the interface shape via a hand-rolled fake that fabricates types with
/// <see cref="System.Reflection.Emit"/> — the contract only cares about the shape of the returned
/// <see cref="Type"/>, not that it came from parsed source text.
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
