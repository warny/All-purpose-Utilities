using System;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Utils.Reflection;

namespace UtilsTest.Reflection;

[TestClass]
public class ExtendedInvokerTests
{
    private static string FromInt(int i) => "int";
    private static string FromDouble(double d) => "double";
    private static string FromStringInt(string s, int i) => "string-int";
    private static string FromObject(object o) => "object";

    [TestMethod]
    public void Invoke_Selects_Best_Delegate()
    {
        var invoker = new ExtendedInvoker<string>();
        invoker.Add((Func<int, string>)FromInt);
        invoker.Add((Func<double, string>)FromDouble);
        invoker.Add((Func<string, int, string>)FromStringInt);
        invoker.Add((Func<object, string>)FromObject);

        Assert.AreEqual("int", invoker.Invoke(10));
        Assert.AreEqual("double", invoker.Invoke(2.5));
        Assert.AreEqual("string-int", invoker.Invoke("foo", 3));
        Assert.AreEqual("object", invoker.Invoke(new object()));
    }

    [TestMethod]
    public void TryInvoke_Returns_False_When_No_Match()
    {
        var invoker = new ExtendedInvoker<string>();
        invoker.Add((Func<int, string>)FromInt);
        var ok = invoker.TryInvoke(["foo"], out var result);

        Assert.IsFalse(ok);
        Assert.IsNull(result);
    }

    // ─── Item 66: ambiguity detection and exception unwrapping ────────────────────

    [TestMethod]
    public void TryInvoke_WhenTwoDelegatesHaveEqualDistance_ThrowsAmbiguousMatchException()
    {
        // Two delegates with the same parameter structure (but different argument types
        // at the same hierarchy level) produce equal distance scores.
        var invoker = new ExtendedInvoker<string>();
        invoker.Add((Func<object, string>)(a => "first"));
        invoker.Add((Func<object, string>)(b => "second"));

        Assert.ThrowsException<AmbiguousMatchException>(
            () => invoker.TryInvoke([new object()], out _),
            "Equal-distance candidates must raise AmbiguousMatchException, not silently pick one.");
    }

    [TestMethod]
    public void Invoke_WhenTwoDelegatesHaveEqualDistance_ThrowsAmbiguousMatchException()
    {
        var invoker = new ExtendedInvoker<string>();
        invoker.Add((Func<object, string>)(a => "first"));
        invoker.Add((Func<object, string>)(b => "second"));

        Assert.ThrowsException<AmbiguousMatchException>(() => invoker.Invoke(new object()));
    }

    [TestMethod]
    public void TryInvoke_WhenDelegateThrows_PropagatesOriginalException()
    {
        // The original exception must not be wrapped in TargetInvocationException.
        var invoker = new ExtendedInvoker<string>();
        invoker.Add((Func<int, string>)(_ => throw new InvalidOperationException("original")));

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => invoker.TryInvoke([1], out _));

        Assert.AreEqual("original", ex.Message,
            "DynamicInvoke wraps exceptions in TargetInvocationException; TryInvoke must unwrap them.");
    }

    [TestMethod]
    public void Invoke_WhenDelegateThrows_PropagatesOriginalException()
    {
        var invoker = new ExtendedInvoker<string>();
        invoker.Add((Func<int, string>)(_ => throw new ArgumentOutOfRangeException("x", "bad value")));

        var ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() => invoker.Invoke(1));

        StringAssert.Contains(ex.Message, "bad value",
            "Invoke must rethrow the original exception, not a TargetInvocationException wrapper.");
    }
}
