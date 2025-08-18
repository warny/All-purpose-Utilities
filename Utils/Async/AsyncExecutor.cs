using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Utils.Async;

/// <summary>
/// Default implementation of <see cref="IAsyncExecutor"/>.
/// </summary>
public class AsyncExecutor : IAsyncExecutor
{
	/// <inheritdoc />
	public async Task ExecuteParallelAsync(IEnumerable<Func<Task>> functions)
	{
		ArgumentNullException.ThrowIfNull(functions);

		var tasks = new List<Task>();
		foreach (var func in functions)
		{
			ArgumentNullException.ThrowIfNull(func);
			tasks.Add(func());
		}

		await Task.WhenAll(tasks);
	}

	/// <inheritdoc />
	public async Task ExecuteSequentialAsync(IEnumerable<Func<Task>> functions)
	{
		ArgumentNullException.ThrowIfNull(functions);

		foreach (var func in functions)
		{
			ArgumentNullException.ThrowIfNull(func);
			await func();
		}
	}

	/// <inheritdoc />
	public async Task ExecuteAsync(IEnumerable<Func<Task>> functions, int parallelThreshold)
	{
		ArgumentNullException.ThrowIfNull(functions);
		ArgumentOutOfRangeException.ThrowIfNegative(parallelThreshold);

		var list = functions as IList<Func<Task>> ?? functions.ToList();

		if (list.Count >= parallelThreshold)
		{
			await ExecuteParallelAsync(list);
		}
		else
		{
			await ExecuteSequentialAsync(list);
		}
	}
}
