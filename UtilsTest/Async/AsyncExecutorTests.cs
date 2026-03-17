using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Utils.Async;

namespace UtilsTest.Async
{
    [TestClass]
    public class AsyncExecutorTests
    {
        [TestMethod]
        public async Task ExecuteSequentialAsyncRunsTasksInOrder()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            List<int> order = [];

            Func<int, Func<Task>> createTask = i => async () =>
            {
                order.Add(i);
                await Task.Delay(10);
            };

            Func<Task>[] tasks =
            [
                createTask(0),
                createTask(1),
                createTask(2),
            ];

            await executor.ExecuteSequentialAsync(tasks);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, order);
        }

        [TestMethod]
        public async Task ExecuteParallelAsyncRunsFasterThanSequential()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task> work() => async () => await Task.Delay(100);
            Func<Task>[] tasks = Enumerable.Range(0, 3).Select(_ => (Func<Task>)work()).ToArray();

            Stopwatch sw = Stopwatch.StartNew();
            await executor.ExecuteSequentialAsync(tasks);
            sw.Stop();
            long sequential = sw.ElapsedMilliseconds;

            sw.Restart();
            await executor.ExecuteParallelAsync(tasks);
            sw.Stop();
            long parallel = sw.ElapsedMilliseconds;

            Assert.IsTrue(parallel < sequential);
        }

        [TestMethod]
        public async Task ExecuteAsyncChoosesParallelWhenCountExceedsThreshold()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task> work() => async () => await Task.Delay(100);
            Func<Task>[] tasks = Enumerable.Range(0, 5).Select(_ => (Func<Task>)work()).ToArray();

            Stopwatch sw = Stopwatch.StartNew();
            await executor.ExecuteAsync(tasks, 3);
            sw.Stop();
            long auto = sw.ElapsedMilliseconds;

            sw.Restart();
            await executor.ExecuteSequentialAsync(tasks);
            sw.Stop();
            long sequential = sw.ElapsedMilliseconds;

            Assert.IsTrue(auto < sequential);
        }

        [TestMethod]
        public async Task ExecuteAsyncChoosesSequentialWhenCountBelowThreshold()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            List<int> order = [];

            Func<int, Func<Task>> createTask = i => async () =>
            {
                order.Add(i);
                await Task.Delay(10);
            };

            Func<Task>[] tasks =
            [
                createTask(0),
                createTask(1),
            ];

            await executor.ExecuteAsync(tasks, 3);

            CollectionAssert.AreEqual(new[] { 0, 1 }, order);
        }

        // ── Null argument validation ────────────────────────────────────────────

        [TestMethod]
        public async Task ExecuteParallelAsync_NullFunctions_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => executor.ExecuteParallelAsync(null!));
        }

        [TestMethod]
        public async Task ExecuteSequentialAsync_NullFunctions_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => executor.ExecuteSequentialAsync(null!));
        }

        [TestMethod]
        public async Task ExecuteAsync_NullFunctions_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => executor.ExecuteAsync(null!, 3));
        }

        [TestMethod]
        public async Task ExecuteAsync_NegativeThreshold_ThrowsArgumentOutOfRangeException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task>[] tasks = [async () => await Task.Delay(1)];
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                () => executor.ExecuteAsync(tasks, -1));
        }

        [TestMethod]
        public async Task ExecuteParallelAsync_NullItemInCollection_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task>[] tasks = [null!];
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => executor.ExecuteParallelAsync(tasks));
        }

        [TestMethod]
        public async Task ExecuteSequentialAsync_NullItemInCollection_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task>[] tasks = [null!];
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => executor.ExecuteSequentialAsync(tasks));
        }

        // ── Exception propagation ───────────────────────────────────────────────

        [TestMethod]
        public async Task ExecuteParallelAsync_TaskThrows_ExceptionPropagates()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task>[] tasks =
            [
                async () => await Task.Delay(1),
                async () => { await Task.Delay(1); throw new InvalidOperationException("boom"); },
            ];

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => executor.ExecuteParallelAsync(tasks));
        }

        [TestMethod]
        public async Task ExecuteSequentialAsync_TaskThrows_ExceptionPropagatesAndStopsExecution()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            int executedCount = 0;

            Func<Task>[] tasks =
            [
                async () => { await Task.Delay(1); executedCount++; },
                async () => { await Task.Delay(1); throw new InvalidOperationException("stop here"); },
                async () => { await Task.Delay(1); executedCount++; },
            ];

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => executor.ExecuteSequentialAsync(tasks));

            // Third task must not have run
            Assert.AreEqual(1, executedCount);
        }
    }
}
