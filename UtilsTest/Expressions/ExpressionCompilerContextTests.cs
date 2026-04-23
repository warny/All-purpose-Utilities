using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Verifies the behavior of <see cref="ExpressionCompilerContext"/>.
/// </summary>
[TestClass]
public class ExpressionCompilerContextTests
{
    /// <summary>
    /// Ensures that symbols set through the public API can be retrieved.
    /// </summary>
    [TestMethod]
    public void SetAndTryGet_ReturnsStoredValue()
    {
        ExpressionCompilerContext context = new();

        context.Set("answer", 42);

        bool found = context.TryGet("answer", out object? value);

        Assert.IsTrue(found);
        Assert.AreEqual(42, value);
    }

    /// <summary>
    /// Ensures that delegate symbols can be invoked dynamically.
    /// </summary>
    [TestMethod]
    public void DynamicInvoke_DelegateSymbol_ReturnsResult()
    {
        dynamic context = new ExpressionCompilerContext();
        context.Add = (Func<int, int, int>)((left, right) => left + right);

        int result = context.Add(2, 3);

        Assert.AreEqual(5, result);
    }

    /// <summary>
    /// Ensures that method groups imported as <see cref="MethodInfo"/> arrays are resolved by argument types.
    /// </summary>
    [TestMethod]
    public void DynamicInvoke_MethodInfoArray_ResolvesMatchingOverload()
    {
        dynamic context = new ExpressionCompilerContext();
        MethodInfo[] methods = typeof(TestMethods)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(TestMethods.Combine))
            .ToArray();

        context.Combine = methods;

        string result = context.Combine("left", "right");

        Assert.AreEqual("left-right", result);
    }

    /// <summary>
    /// Ensures that stream persistence preserves scalar values and callable symbols.
    /// </summary>
    [TestMethod]
    public void WriteToStreamAndReadFromStream_PreservesValuesAndFunctions()
    {
        ExpressionCompilerContext source = new();
        source.Set("answer", 42);
        source.Set("add", (Func<int, int, int>)TestMethods.Combine);
        source.Set("combine", typeof(TestMethods)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(TestMethods.Combine))
            .ToArray());

        using MemoryStream stream = new();
        source.WriteToStream(stream);
        stream.Position = 0;

        dynamic restored = ExpressionCompilerContext.ReadFromStream(stream);

        Assert.AreEqual(42, restored.answer);
        Assert.AreEqual(7, restored.add(3, 4));
        Assert.AreEqual("left-right", restored.combine("left", "right"));
    }

    /// <summary>
    /// Provides overloaded methods used by invocation tests.
    /// </summary>
    private static class TestMethods
    {
        /// <summary>
        /// Combines two strings.
        /// </summary>
        /// <param name="left">Left value.</param>
        /// <param name="right">Right value.</param>
        /// <returns>A combined value.</returns>
        public static string Combine(string left, string right) => $"{left}-{right}";

        /// <summary>
        /// Combines two integers.
        /// </summary>
        /// <param name="left">Left value.</param>
        /// <param name="right">Right value.</param>
        /// <returns>The sum of both values.</returns>
        public static int Combine(int left, int right) => left + right;
    }
}
