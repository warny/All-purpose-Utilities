using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utils.IO.Serialization;

namespace UtilsTest.Serialization;

/// <summary>Verifies shared contract-cache state without timing-dependent concurrency.</summary>
[TestClass]
public sealed class ContractCacheConcurrencyTests
{
    /// <summary>Ensures two owners that discover opposite dependency edges fail instead of deadlocking.</summary>
    [TestMethod]
    public async Task InterThreadCycle_IsDetectedBeforeMutualWait()
    {
        var cache = new ContractCache();
        using var ownersReady = new Barrier(2);
        Delegate Build(Type type)
        {
            ownersReady.SignalAndWait();
            Type dependency = type == typeof(CycleA) ? typeof(CycleB) : typeof(CycleA);
            return cache.GetOrBuild(dependency, Build);
        }

        Task<Exception?>[] builds =
        [
            Task.Run(() => Capture(() => cache.GetOrBuild(typeof(CycleA), Build))),
            Task.Run(() => Capture(() => cache.GetOrBuild(typeof(CycleB), Build)))
        ];
        Exception?[] failures = await Task.WhenAll(builds).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(failures.All(error => error is SerializationContractException));
        Assert.IsTrue(failures.All(error => error is not StackOverflowException));
        Assert.IsTrue(failures.Any(error => error!.Message.Contains(nameof(CycleA), StringComparison.Ordinal) && error.Message.Contains(nameof(CycleB), StringComparison.Ordinal)));
    }

    /// <summary>Ensures concurrent first use of one type performs one logical build.</summary>
    [TestMethod]
    public async Task SameType_IsBuiltOnceAndShared()
    {
        var cache = new ContractCache();
        using var callersReady = new Barrier(4);
        var buildEntered = new ManualResetEventSlim();
        var allowBuild = new ManualResetEventSlim();
        int buildCount = 0;
        Delegate Build(Type _)
        {
            Interlocked.Increment(ref buildCount);
            buildEntered.Set();
            allowBuild.Wait();
            return (Action)(() => { });
        }
        Task<Delegate>[] callers = Enumerable.Range(0, 4).Select(_ => Task.Factory.StartNew(() =>
            {
                callersReady.SignalAndWait();
                return cache.GetOrBuild(typeof(IndependentA), Build);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        buildEntered.Wait();
        allowBuild.Set();
        Delegate[] results = await Task.WhenAll(callers);

        Assert.AreEqual(1, buildCount);
        Assert.IsTrue(results.All(result => ReferenceEquals(result, results[0])));
    }

    /// <summary>Ensures independent contract builders can execute concurrently.</summary>
    [TestMethod]
    public async Task IndependentTypes_BuildInParallel()
    {
        var cache = new ContractCache();
        using var buildersTogether = new Barrier(2);
        Delegate Build(Type _) { buildersTogether.SignalAndWait(); return (Action)(() => { }); }

        await Task.WhenAll(
            Task.Run(() => cache.GetOrBuild(typeof(IndependentA), Build)),
            Task.Run(() => cache.GetOrBuild(typeof(IndependentB), Build))).WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>Captures a synchronous build failure without wrapping it.</summary>
    private static Exception? Capture(Action action)
    {
        try { action(); return null; }
        catch (Exception error) { return error; }
    }

    /// <summary>First cyclic cache key.</summary>
    private sealed class CycleA { }

    /// <summary>Second cyclic cache key.</summary>
    private sealed class CycleB { }

    /// <summary>First independent cache key.</summary>
    private sealed class IndependentA { }

    /// <summary>Second independent cache key.</summary>
    private sealed class IndependentB { }
}
