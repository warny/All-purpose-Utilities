using System;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Validates that <see cref="MathMethodResolver"/> resolves supported scalar operations and reports
/// unsupported ones through an explicit diagnostic instead of a raw reflection null.
/// </summary>
[TestClass]
public class MathMethodResolverTests
{
    /// <summary>
    /// A supported single-argument static method (<see cref="double.Log(double)"/>) resolves to a
    /// callable <see cref="System.Reflection.MethodInfo"/>.
    /// </summary>
    [TestMethod]
    public void Resolve_SupportedDoubleMethod_ReturnsCallableMethod()
    {
        var method = MathMethodResolver.Resolve<double>(nameof(double.Log));

        var x = Expression.Parameter(typeof(double), "x");
        var call = Expression.Lambda<Func<double, double>>(Expression.Call(method, x), x).Compile();

        Assert.AreEqual(Math.Log(2.0), call(2.0), 1e-12);
    }

    /// <summary>
    /// <see cref="decimal"/> satisfies <see cref="System.Numerics.IFloatingPoint{TSelf}"/> but declares no
    /// transcendental functions; resolving one must fail with a dedicated
    /// <see cref="UnsupportedScalarOperationException"/> (a <see cref="NotSupportedException"/> subclass)
    /// naming both the operation and the type rather than an incidental null-reference failure deep inside
    /// <see cref="Expression.Call(System.Reflection.MethodInfo, Expression[])"/>.
    /// </summary>
    [TestMethod]
    public void Resolve_UnsupportedScalarType_ThrowsNotSupportedExceptionWithDiagnostic()
    {
        var ex = Assert.ThrowsExactly<UnsupportedScalarOperationException>(() => MathMethodResolver.Resolve<decimal>(nameof(double.Log)));

        StringAssert.Contains(ex.Message, "Log");
        StringAssert.Contains(ex.Message, "Decimal");
    }

    /// <summary>
    /// Failed lookups are cached: a second call for the same unsupported (type, operation) pair still
    /// throws the same diagnostic instead of erroring differently or crashing on repeated reflection.
    /// </summary>
    [TestMethod]
    public void Resolve_UnsupportedScalarType_IsConsistentAcrossRepeatedCalls()
    {
        var first = Assert.ThrowsExactly<UnsupportedScalarOperationException>(() => MathMethodResolver.Resolve<decimal>(nameof(double.Sin)));
        var second = Assert.ThrowsExactly<UnsupportedScalarOperationException>(() => MathMethodResolver.Resolve<decimal>(nameof(double.Sin)));

        Assert.AreEqual(first.Message, second.Message);
    }
}
