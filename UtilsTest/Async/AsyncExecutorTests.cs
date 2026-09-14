using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
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

            Func<int, Func<Task>> createTask = i => () =>
            {
                order.Add(i);
                return Task.CompletedTask;
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

        /// <summary>
        /// Proves that parallel execution starts every task before any task is released.
        /// </summary>
        [TestMethod]
        public async Task ExecuteParallelAsync_StartsAllTasksBeforeAnyIsReleased()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            CountdownEvent entered = new(3);
            Func<Task> CreateWork() => async () =>
            {
                entered.Signal();
                await release.Task;
            };
            Func<Task>[] tasks = [CreateWork(), CreateWork(), CreateWork()];

            Task execution = executor.ExecuteParallelAsync(tasks);
            try
            {
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)), "All parallel tasks must enter before release.");
            }
            finally
            {
                release.TrySetResult();
            }
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Proves that automatic execution selects parallel mode above the threshold.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsyncChoosesParallelWhenCountExceedsThreshold()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            CountdownEvent entered = new(5);
            Func<Task> CreateWork() => async () =>
            {
                entered.Signal();
                await release.Task;
            };
            Func<Task>[] tasks = [CreateWork(), CreateWork(), CreateWork(), CreateWork(), CreateWork()];

            Task execution = executor.ExecuteAsync(tasks, 3);
            try
            {
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)), "Automatic parallel mode must start every task before release.");
            }
            finally
            {
                release.TrySetResult();
            }
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Proves that automatic execution keeps the second task out while the first sequential task is blocked.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsyncChoosesSequentialWhenCountBelowThreshold()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
            bool secondEntered = false;
            Func<Task>[] tasks =
            [
                async () => { firstEntered.TrySetResult(); await releaseFirst.Task; },
                () => { secondEntered = true; return Task.CompletedTask; }
            ];

            Task execution = executor.ExecuteAsync(tasks, 3);
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(secondEntered, "The second task cannot enter while the first sequential task is blocked.");
            releaseFirst.TrySetResult();
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(secondEntered);
        }

        // ── Null argument validation ────────────────────────────────────────────

        [TestMethod]
        public async Task ExecuteParallelAsync_NullFunctions_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => executor.ExecuteParallelAsync(null!));
        }

        [TestMethod]
        public async Task ExecuteSequentialAsync_NullFunctions_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => executor.ExecuteSequentialAsync(null!));
        }

        [TestMethod]
        public async Task ExecuteAsync_NullFunctions_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => executor.ExecuteAsync(null!, 3));
        }

        [TestMethod]
        public async Task ExecuteAsync_NegativeThreshold_ThrowsArgumentOutOfRangeException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task>[] tasks = [() => Task.CompletedTask];
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
                () => executor.ExecuteAsync(tasks, -1));
        }

        [TestMethod]
        public async Task ExecuteParallelAsync_NullItemInCollection_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task>[] tasks = [null!];
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => executor.ExecuteParallelAsync(tasks));
        }

        [TestMethod]
        public async Task ExecuteSequentialAsync_NullItemInCollection_ThrowsArgumentNullException()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task>[] tasks = [null!];
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => executor.ExecuteSequentialAsync(tasks));
        }

        // ── Exception propagation ───────────────────────────────────────────────

        [TestMethod]
        public async Task ExecuteParallelAsync_TaskThrows_ExceptionPropagates()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            Func<Task>[] tasks =
            [
                () => Task.CompletedTask,
                () => Task.FromException(new InvalidOperationException("boom")),
            ];

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => executor.ExecuteParallelAsync(tasks));
        }

        [TestMethod]
        public async Task ExecuteSequentialAsync_TaskThrows_ExceptionPropagatesAndStopsExecution()
        {
            IAsyncExecutor executor = new AsyncExecutor();
            int executedCount = 0;

            Func<Task>[] tasks =
            [
                () => { executedCount++; return Task.CompletedTask; },
                () => Task.FromException(new InvalidOperationException("stop here")),
                () => { executedCount++; return Task.CompletedTask; },
            ];

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => executor.ExecuteSequentialAsync(tasks));

            // Third task must not have run
            Assert.AreEqual(1, executedCount);
        }
    }
}
