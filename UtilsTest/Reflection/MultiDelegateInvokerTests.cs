using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Utils.Reflection;

namespace UtilsTest.Reflection;

[TestClass]
public class MultiDelegateInvokerTests
{
    private static int AddOne(int i) => i + 1;
    private static int AddTwo(int i) => i + 2;

    /// <summary>
    /// Ensures the thread pool can schedule at least <paramref name="minimumWorkerThreads"/> workers,
    /// executes <paramref name="action"/>, then restores the original pool settings.
    /// </summary>
    /// <param name="minimumWorkerThreads">Minimum worker thread count required for the test.</param>
    /// <param name="action">Asynchronous test action.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task WithMinWorkerThreadsAsync(int minimumWorkerThreads, Func<Task> action)
    {
        ThreadPool.GetMinThreads(out int originalWorker, out int originalIo);
        int targetWorker = Math.Max(originalWorker, minimumWorkerThreads);
        ThreadPool.SetMinThreads(targetWorker, originalIo);
        try
        {
            await action();
        }
        finally
        {
            ThreadPool.SetMinThreads(originalWorker, originalIo);
        }
    }

    [TestMethod]
    public void Invoke_Returns_All_Results()
    {
        var invoker = new MultiDelegateInvoker<int, int>();
        invoker.Add<int>(AddOne);
        invoker.Add<int>(AddTwo);

        int[] results = invoker.Invoke(3);
        CollectionAssert.AreEqual(new[] { 4, 5 }, results);
    }

    [TestMethod]
    public async Task InvokeAsync_Returns_All_Results()
    {
        var invoker = new MultiDelegateInvoker<int, int>();
        invoker.Add<int>(AddOne);
        invoker.Add<int>(AddTwo);

        int[] results = await invoker.InvokeAsync(3);
        CollectionAssert.AreEqual(new[] { 4, 5 }, results);
    }

    [TestMethod]
    public async Task InvokeParallelAsync_Executes_In_Parallel()
    {
        await WithMinWorkerThreadsAsync(8, async () =>
        {
            var invoker = new MultiDelegateInvoker<int, int>();
            ManualResetEventSlim startGate = new(false);
            int current = 0;
            int maxConcurrent = 0;

            Func<int, int> CreateDelegate(int offset) => i =>
            {
                startGate.Wait();
                int now = Interlocked.Increment(ref current);
                InterlockedExtensions.MaxExchange(ref maxConcurrent, now);
                Thread.Sleep(200);
                Interlocked.Decrement(ref current);
                return i + offset;
            };

            invoker.Add<int>(CreateDelegate(1));
            invoker.Add<int>(CreateDelegate(2));
            invoker.Add<int>(CreateDelegate(3));

            Task<int[]> invokeTask = invoker.InvokeParallelAsync(3);
            await Task.Delay(100);
            startGate.Set();
            int[] results = await invokeTask;

            CollectionAssert.AreEqual(new[] { 4, 5, 6 }, results);
            Assert.IsTrue(maxConcurrent >= 2, $"Expected parallel execution but max concurrency was {maxConcurrent}.");
        });
    }

    [TestMethod]
    public async Task InvokeSmartAsync_Switches_Based_On_Threshold()
    {
        await WithMinWorkerThreadsAsync(8, async () =>
        {
            var sequential = new MultiDelegateInvoker<int, int>(4);
            ManualResetEventSlim sequentialGate = new(true);
            int sequentialCurrent = 0;
            int sequentialMax = 0;

            Func<int, int> CreateSequentialDelegate(int offset) => i =>
            {
                sequentialGate.Wait();
                int now = Interlocked.Increment(ref sequentialCurrent);
                InterlockedExtensions.MaxExchange(ref sequentialMax, now);
                Thread.Sleep(200);
                Interlocked.Decrement(ref sequentialCurrent);
                return i + offset;
            };

            sequential.Add<int>(CreateSequentialDelegate(1));
            sequential.Add<int>(CreateSequentialDelegate(2));
            sequential.Add<int>(CreateSequentialDelegate(3));
            await sequential.InvokeSmartAsync(3);

            ManualResetEventSlim parallelGate = new(false);
            int parallelCurrent = 0;
            int parallelMax = 0;

            Func<int, int> CreateParallelDelegate(int offset) => i =>
            {
                parallelGate.Wait();
                int now = Interlocked.Increment(ref parallelCurrent);
                InterlockedExtensions.MaxExchange(ref parallelMax, now);
                Thread.Sleep(200);
                Interlocked.Decrement(ref parallelCurrent);
                return i + offset;
            };

            var parallel = new MultiDelegateInvoker<int, int>(1);
            parallel.Add<int>(CreateParallelDelegate(1));
            parallel.Add<int>(CreateParallelDelegate(2));
            parallel.Add<int>(CreateParallelDelegate(3));

            Task parallelInvocation = parallel.InvokeSmartAsync(3);
            await Task.Delay(100);
            parallelGate.Set();
            await parallelInvocation;

            Assert.AreEqual(1, sequentialMax, $"Sequential mode executed with max concurrency {sequentialMax}.");
            Assert.IsTrue(parallelMax >= 2, $"Parallel mode executed with max concurrency {parallelMax}.");
        });
    }
}


/// <summary>
/// Provides atomic helper operations for test concurrency metrics.
/// </summary>
internal static class InterlockedExtensions
{
    /// <summary>
    /// Atomically stores the maximum value observed so far in <paramref name="target"/>.
    /// </summary>
    /// <param name="target">Target integer updated in a lock-free way.</param>
    /// <param name="candidate">Candidate value to compare and store when greater.</param>
    public static void MaxExchange(ref int target, int candidate)
    {
        int snapshot;
        do
        {
            snapshot = Volatile.Read(ref target);
            if (candidate <= snapshot)
                return;
        }
        while (Interlocked.CompareExchange(ref target, candidate, snapshot) != snapshot);
    }
}
