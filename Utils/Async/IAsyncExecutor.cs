using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Utils.Async;

/// <summary>
/// Executes asynchronous delegates either sequentially or in parallel.
/// </summary>
public interface IAsyncExecutor
{
/// <summary>
/// Runs all provided asynchronous delegates in parallel.
/// </summary>
/// <param name="functions">Delegates to execute.</param>
/// <returns>A task that completes once all delegates have finished.</returns>
Task ExecuteParallelAsync(IEnumerable<Func<Task>> functions);

/// <summary>
/// Runs all provided asynchronous delegates sequentially in the order they appear.
/// </summary>
/// <param name="functions">Delegates to execute.</param>
/// <returns>A task that completes once all delegates have finished.</returns>
Task ExecuteSequentialAsync(IEnumerable<Func<Task>> functions);

/// <summary>
/// Executes delegates sequentially or in parallel depending on the <paramref name="parallelThreshold"/>.
/// </summary>
/// <param name="functions">Delegates to execute.</param>
/// <param name="parallelThreshold">Minimum number of delegates required to switch to parallel execution.</param>
/// <returns>A task that completes once all delegates have finished.</returns>
Task ExecuteAsync(IEnumerable<Func<Task>> functions, int parallelThreshold);
}
