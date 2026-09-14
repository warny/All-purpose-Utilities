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

    /// <summary>
    /// Proves that at least two parallel delegates enter before the shared release gate opens.
    /// </summary>
    [TestMethod]
    public async Task InvokeParallelAsync_Executes_In_Parallel()
    {
        MultiDelegateInvoker<int, int> invoker = new();
        int enteredCount = 0;
        using ManualResetEventSlim twoEntered = new(false);
        using ManualResetEventSlim release = new(false);
        Func<int, int> CreateDelegate(int offset) => value =>
        {
            if (Interlocked.Increment(ref enteredCount) == 2)
                twoEntered.Set();
            release.Wait();
            return value + offset;
        };
        invoker.Add<int>(CreateDelegate(1));
        invoker.Add<int>(CreateDelegate(2));
        invoker.Add<int>(CreateDelegate(3));

        Task<int[]> invocation = invoker.InvokeParallelAsync(3);
        try
        {
            Assert.IsTrue(twoEntered.Wait(TimeSpan.FromSeconds(5)), "At least two delegates must enter before release.");
        }
        finally
        {
            release.Set();
        }

        CollectionAssert.AreEqual(new[] { 4, 5, 6 }, await invocation.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// Proves that smart invocation selects sequential and parallel threshold branches.
    /// </summary>
    [TestMethod]
    public async Task InvokeSmartAsync_Switches_Based_On_Threshold()
    {
        MultiDelegateInvoker<int, int> sequential = new(4);
        using ManualResetEventSlim firstEntered = new(false);
        using ManualResetEventSlim releaseFirst = new(false);
        using ManualResetEventSlim secondEntered = new(false);
        sequential.Add<int>(value => { firstEntered.Set(); releaseFirst.Wait(); return value + 1; });
        sequential.Add<int>(value => { secondEntered.Set(); return value + 2; });
        sequential.Add<int>(value => value + 3);

        Task<int[]> sequentialInvocation = sequential.InvokeSmartAsync(3);
        try
        {
            Assert.IsTrue(firstEntered.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(secondEntered.IsSet, "A later sequential delegate cannot enter while the first is blocked.");
        }
        finally
        {
            releaseFirst.Set();
        }
        CollectionAssert.AreEqual(new[] { 4, 5, 6 }, await sequentialInvocation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(secondEntered.IsSet);

        MultiDelegateInvoker<int, int> parallel = new(1);
        int parallelEnteredCount = 0;
        using ManualResetEventSlim twoParallelEntered = new(false);
        using ManualResetEventSlim releaseParallel = new(false);
        Func<int, int> CreateParallelDelegate(int offset) => value =>
        {
            if (Interlocked.Increment(ref parallelEnteredCount) == 2)
                twoParallelEntered.Set();
            releaseParallel.Wait();
            return value + offset;
        };
        parallel.Add<int>(CreateParallelDelegate(1));
        parallel.Add<int>(CreateParallelDelegate(2));
        parallel.Add<int>(CreateParallelDelegate(3));

        Task<int[]> parallelInvocation = parallel.InvokeSmartAsync(3);
        try
        {
            Assert.IsTrue(twoParallelEntered.Wait(TimeSpan.FromSeconds(5)), "Smart parallel mode must overlap at least two delegates before release.");
        }
        finally
        {
            releaseParallel.Set();
        }
        CollectionAssert.AreEqual(new[] { 4, 5, 6 }, await parallelInvocation.WaitAsync(TimeSpan.FromSeconds(5)));
    }

}
