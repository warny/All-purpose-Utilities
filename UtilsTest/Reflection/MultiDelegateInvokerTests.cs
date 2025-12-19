using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Threading.Tasks;
using Utils.Reflection;

namespace UtilsTest.Reflection;

/// <summary>
/// Tests for <see cref="MultiDelegateInvoker{T, TResult}"/> behavior.
/// </summary>
[TestClass]
public class MultiDelegateInvokerTests
{
    /// <summary>
    /// Adds one to the provided integer value.
    /// </summary>
    /// <param name="i">The input value.</param>
    /// <returns>The input value incremented by one.</returns>
    private static int AddOne(int i) => i + 1;

    /// <summary>
    /// Adds two to the provided integer value.
    /// </summary>
    /// <param name="i">The input value.</param>
    /// <returns>The input value incremented by two.</returns>
    private static int AddTwo(int i) => i + 2;

    /// <summary>
    /// Measures the execution time of a delegate invoker call.
    /// </summary>
    /// <param name="invocation">The invocation to measure.</param>
    /// <returns>The invocation results and elapsed time in milliseconds.</returns>
    private static async Task<(int[] Results, long ElapsedMilliseconds)> MeasureInvocationAsync(Func<Task<int[]>> invocation)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int[] results = await invocation();
        stopwatch.Stop();

        return (results, stopwatch.ElapsedMilliseconds);
    }

    [TestMethod]
    /// <summary>
    /// Ensures that synchronous invocation returns every delegate result in order.
    /// </summary>
    public void Invoke_Returns_All_Results()
    {
        var invoker = new MultiDelegateInvoker<int, int>();
        invoker.Add<int>(AddOne);
        invoker.Add<int>(AddTwo);

        int[] results = invoker.Invoke(3);
        CollectionAssert.AreEqual(new[] { 4, 5 }, results);
    }

    [TestMethod]
    /// <summary>
    /// Ensures that asynchronous invocation returns every delegate result in order.
    /// </summary>
    public async Task InvokeAsync_Returns_All_Results()
    {
        var invoker = new MultiDelegateInvoker<int, int>();
        invoker.Add<int>(AddOne);
        invoker.Add<int>(AddTwo);

        int[] results = await invoker.InvokeAsync(3);
        CollectionAssert.AreEqual(new[] { 4, 5 }, results);
    }

    [TestMethod]
    /// <summary>
    /// Verifies that parallel invocation completes faster than sequential execution while returning all results.
    /// </summary>
    public async Task InvokeParallelAsync_Executes_In_Parallel()
    {
        var invoker = new MultiDelegateInvoker<int, int>();
        invoker.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 1; });
        invoker.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 2; });

        (int[] parallelResults, long parallelDuration) = await MeasureInvocationAsync(() => invoker.InvokeParallelAsync(3));
        (int[] sequentialResults, long sequentialDuration) = await MeasureInvocationAsync(() => invoker.InvokeAsync(3));

        CollectionAssert.AreEqual(new[] { 4, 5 }, parallelResults);
        CollectionAssert.AreEqual(new[] { 4, 5 }, sequentialResults);
        Assert.IsTrue(parallelDuration + 30 < sequentialDuration);
    }

    [TestMethod]
    /// <summary>
    /// Confirms that the smart invocation strategy switches between sequential and parallel execution based on the configured threshold.
    /// </summary>
    public async Task InvokeSmartAsync_Switches_Based_On_Threshold()
    {
        var sequential = new MultiDelegateInvoker<int, int>(3);
        sequential.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 1; });
        sequential.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 2; });
        Stopwatch sw1 = Stopwatch.StartNew();
        await sequential.InvokeSmartAsync(3);
        sw1.Stop();
        Assert.IsTrue(sw1.ElapsedMilliseconds >= 190);

        var parallel = new MultiDelegateInvoker<int, int>(1);
        parallel.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 1; });
        parallel.Add<int>(i => { System.Threading.Thread.Sleep(100); return i + 2; });
        Stopwatch sw2 = Stopwatch.StartNew();
        await parallel.InvokeSmartAsync(3);
        sw2.Stop();
        Assert.IsTrue(sw2.ElapsedMilliseconds < 190);
    }
}

