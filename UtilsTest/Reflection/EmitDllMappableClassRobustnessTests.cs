using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection.Reflection.Emit;

namespace UtilsTest.Reflection;

/// <summary>Concrete (non-interface) type used to verify <see cref="EmitDllMappableClass.Emit(Type, CallingConvention)"/> rejects it.</summary>
public class EmitDllMappableClassRobustnessNotAnInterface : IDisposable
{
    public void Dispose() { }
}

/// <summary>Interface exercising <c>ref</c>/<c>out</c> parameter code generation.</summary>
public interface IEmitDllMappableClassRefOutTarget : IDisposable
{
    int Compute(ref int accumulator, out int doubled);
}

/// <summary>
/// Interface exercising an explicit <c>[Out]</c> attribute on a non-byref parameter (the common
/// <c>StringBuilder</c> P/Invoke idiom) — distinct from a genuine <c>out</c> keyword parameter, and
/// must keep the attribute in the generated declaration rather than dropping it.
/// </summary>
public interface IEmitDllMappableClassOutAttributeTarget : IDisposable
{
    int Fill(int capacity, [Out] StringBuilder buffer);
}

/// <summary>Interface used purely as a concurrent-Emit stress target (its native function is never invoked).</summary>
public interface IEmitDllMappableClassConcurrentTarget : IDisposable
{
    int Ping(int value);
}

/// <summary>
/// Interface with a <see langword="void"/>-returning method, used to verify the generated mapping class
/// compiles: <c>Type.FullName</c> for <see langword="void"/> is the CLR metadata name
/// <c>"System.Void"</c>, not valid C# source in a return-type position.
/// </summary>
public interface IEmitDllMappableClassVoidReturnTarget : IDisposable
{
    void DoSomething(int value);
}

/// <summary>Generic interface used to verify <see cref="EmitDllMappableClass.Emit(Type, CallingConvention)"/> rejects it upfront.</summary>
public interface IEmitDllMappableClassGenericTarget<T> : IDisposable
{
    T Identity(T value);
}

/// <summary>Interface with a generic method, used to verify it is rejected upfront rather than reaching Roslyn.</summary>
public interface IEmitDllMappableClassGenericMethodTarget : IDisposable
{
    T Identity<T>(T value);
}

/// <summary>
/// Robustness tests for <see cref="EmitDllMappableClass"/>: rejecting non-interface types,
/// generating code for <c>ref</c>/<c>out</c> parameters, and concurrent <c>Emit</c> calls for the
/// same (interface, calling convention) key racing on the cache (see items 10-11 of the audit).
/// None of these need a real native DLL: <see cref="EmitDllMappableClass.Emit(Type, CallingConvention)"/>
/// only compiles and instantiates the mapping class — wiring delegate fields to actual exports is a
/// separate step performed by <c>LibraryMapper.EmitCore</c>.
/// </summary>
[TestClass]
public class EmitDllMappableClassRobustnessTests
{
    [TestMethod]
    public void Emit_NonInterfaceType_ThrowsNotSupportedException()
    {
#pragma warning disable UTILSREFL001 // Deliberately exercising the in-process codegen path under test.
        NotSupportedException ex = Assert.ThrowsException<NotSupportedException>(
            () => EmitDllMappableClass.Emit(typeof(EmitDllMappableClassRobustnessNotAnInterface), CallingConvention.Cdecl));
#pragma warning restore UTILSREFL001

        StringAssert.Contains(ex.Message, "interface");
    }

    [TestMethod]
    public void Emit_GenericInterface_ThrowsNotSupportedException()
    {
#pragma warning disable UTILSREFL001 // Deliberately exercising the in-process codegen path under test.
        NotSupportedException ex = Assert.ThrowsException<NotSupportedException>(
            () => EmitDllMappableClass.Emit(typeof(IEmitDllMappableClassGenericTarget<int>), CallingConvention.Cdecl));
#pragma warning restore UTILSREFL001

        StringAssert.Contains(ex.Message, "generic");
    }

    [TestMethod]
    public void Emit_InterfaceWithGenericMethod_ThrowsNotSupportedException()
    {
#pragma warning disable UTILSREFL001
        NotSupportedException ex = Assert.ThrowsException<NotSupportedException>(
            () => EmitDllMappableClass.Emit(typeof(IEmitDllMappableClassGenericMethodTarget), CallingConvention.Cdecl));
#pragma warning restore UTILSREFL001

        StringAssert.Contains(ex.Message, "generic");
    }

    [TestMethod]
    public void Emit_InterfaceWithRefOutParameters_GeneratesAndInstantiatesWithoutError()
    {
#pragma warning disable UTILSREFL001
        object instance = EmitDllMappableClass.Emit(typeof(IEmitDllMappableClassRefOutTarget), CallingConvention.Cdecl);
#pragma warning restore UTILSREFL001

        Assert.IsNotNull(instance);
        Assert.IsInstanceOfType(instance, typeof(IEmitDllMappableClassRefOutTarget));
    }

    [TestMethod]
    public void Emit_InterfaceWithVoidReturningMethod_GeneratesAndInstantiatesWithoutError()
    {
        // Regression test: Type.FullName for typeof(void) is "System.Void", which used to be written
        // verbatim into the generated delegate/method return type — invalid C# source ("System.Void"
        // is not usable as a return type; only the "void" keyword is), failing Roslyn compilation for
        // every interface with a void-returning method. Discovered by
        // EmitWorkerHostLoopTests.Run_ExecutesTwoSlowCallsConcurrently_NotSequentially (item 34), which
        // was the first test in the suite to exercise a void-returning native export.
#pragma warning disable UTILSREFL001
        object instance = EmitDllMappableClass.Emit(typeof(IEmitDllMappableClassVoidReturnTarget), CallingConvention.Cdecl);
#pragma warning restore UTILSREFL001

        Assert.IsNotNull(instance);
        Assert.IsInstanceOfType(instance, typeof(IEmitDllMappableClassVoidReturnTarget));
    }

    [TestMethod]
    public void Emit_InterfaceWithNonByRefOutAttribute_GeneratesAndInstantiatesWithoutError()
    {
#pragma warning disable UTILSREFL001
        object instance = EmitDllMappableClass.Emit(typeof(IEmitDllMappableClassOutAttributeTarget), CallingConvention.Cdecl);
#pragma warning restore UTILSREFL001

        Assert.IsNotNull(instance);
        Assert.IsInstanceOfType(instance, typeof(IEmitDllMappableClassOutAttributeTarget));
    }

    [TestMethod]
    public void Emit_ConcurrentCallsForSameInterface_AllSucceedWithoutThrowing()
    {
        var tasks = new List<Task<object>>();
        for (int i = 0; i < 16; i++)
        {
            tasks.Add(Task.Run(object () =>
            {
#pragma warning disable UTILSREFL001
                return EmitDllMappableClass.Emit(typeof(IEmitDllMappableClassConcurrentTarget), CallingConvention.Cdecl);
#pragma warning restore UTILSREFL001
            }));
        }

        Task.WaitAll(tasks.ToArray());

        foreach (Task<object> task in tasks)
        {
            Assert.IsInstanceOfType(task.Result, typeof(IEmitDllMappableClassConcurrentTarget));
        }
    }
}
