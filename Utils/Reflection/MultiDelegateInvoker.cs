using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Reflection;

/// <summary>
/// Invokes multiple delegates registered for a given argument type.
/// </summary>
/// <typeparam name="TBaseArg">The base type for the argument of the delegates.</typeparam>
/// <typeparam name="TResult">The return type of the delegates.</typeparam>
public class MultiDelegateInvoker<TBaseArg, TResult> : IEnumerable<Delegate>
{
private readonly Dictionary<Type, List<Delegate>> _delegates = new();
private readonly int _parallelThreshold;

/// <summary>
/// Initializes a new instance of the <see cref="MultiDelegateInvoker{TBaseArg, TResult}"/> class.
/// </summary>
/// <param name="parallelThreshold">Minimum number of delegates required to run in parallel when using <see cref="InvokeSmartAsync"/>.</param>
/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="parallelThreshold"/> is less than one.</exception>
public MultiDelegateInvoker(int parallelThreshold = 4)
{
if (parallelThreshold < 1) throw new ArgumentOutOfRangeException(nameof(parallelThreshold));
_parallelThreshold = parallelThreshold;
}

/// <summary>
/// Registers a delegate function for a specific argument type derived from <typeparamref name="TBaseArg"/>.
/// </summary>
/// <typeparam name="T">The argument type for the delegate.</typeparam>
/// <param name="function">The delegate to register.</param>
public void Add<T>(Func<T, TResult> function) where T : TBaseArg
{
ArgumentNullException.ThrowIfNull(function);
if (!_delegates.TryGetValue(typeof(T), out var list))
{
list = [];
_delegates[typeof(T)] = list;
}
list.Add(function);
}

private List<Delegate> GetDelegates(TBaseArg arg)
{
if (arg == null) throw new ArgumentNullException(nameof(arg));

List<Delegate> list = null;
Type type = arg.GetType();
while (type != null && !_delegates.TryGetValue(type, out list))
{
type = type.BaseType;
}

return list ?? [];
}

/// <summary>
/// Invokes the registered delegates sequentially for the runtime type of <paramref name="arg"/>.
/// </summary>
/// <param name="arg">The argument to pass to each delegate.</param>
/// <returns>An array containing the results of each delegate call.</returns>
/// <exception cref="MissingMethodException">Thrown when no delegate is registered for the argument type.</exception>
public TResult[] Invoke(TBaseArg arg)
{
List<Delegate> list = GetDelegates(arg);
if (list.Count == 0)
throw new MissingMethodException($"No delegate is registered for type {arg.GetType()} or its base types.");

TResult[] results = new TResult[list.Count];
for (int i = 0; i < list.Count; i++)
results[i] = (TResult)list[i].DynamicInvoke(arg);
return results;
}

/// <summary>
/// Invokes the registered delegates sequentially on background threads.
/// </summary>
/// <param name="arg">The argument to pass to each delegate.</param>
/// <param name="cancellationToken">A token used to cancel the operation.</param>
/// <returns>A task producing an array with the results of each delegate call.</returns>
/// <exception cref="MissingMethodException">Thrown when no delegate is registered for the argument type.</exception>
public async Task<TResult[]> InvokeAsync(TBaseArg arg, CancellationToken cancellationToken = default)
{
List<Delegate> list = GetDelegates(arg);
if (list.Count == 0)
throw new MissingMethodException($"No delegate is registered for type {arg.GetType()} or its base types.");

TResult[] results = new TResult[list.Count];
for (int i = 0; i < list.Count; i++)
{
cancellationToken.ThrowIfCancellationRequested();
Delegate del = list[i];
results[i] = await Task.Run(() => (TResult)del.DynamicInvoke(arg), cancellationToken).ConfigureAwait(false);
}

return results;
}

/// <summary>
/// Invokes the registered delegates in parallel.
/// </summary>
/// <param name="arg">The argument to pass to each delegate.</param>
/// <param name="cancellationToken">A token used to cancel the operation.</param>
/// <returns>A task producing an array with the results of each delegate call.</returns>
/// <exception cref="MissingMethodException">Thrown when no delegate is registered for the argument type.</exception>
public async Task<TResult[]> InvokeParallelAsync(TBaseArg arg, CancellationToken cancellationToken = default)
{
List<Delegate> list = GetDelegates(arg);
if (list.Count == 0)
throw new MissingMethodException($"No delegate is registered for type {arg.GetType()} or its base types.");

Task<TResult>[] tasks = new Task<TResult>[list.Count];
for (int i = 0; i < list.Count; i++)
{
Delegate del = list[i];
tasks[i] = Task.Run(() =>
{
cancellationToken.ThrowIfCancellationRequested();
return (TResult)del.DynamicInvoke(arg);
}, cancellationToken);
}

return await Task.WhenAll(tasks).ConfigureAwait(false);
}

/// <summary>
/// Invokes the registered delegates either sequentially or in parallel depending on the configured threshold.
/// </summary>
/// <param name="arg">The argument to pass to each delegate.</param>
/// <param name="cancellationToken">A token used to cancel the operation.</param>
/// <returns>A task producing an array with the results of each delegate call.</returns>
public Task<TResult[]> InvokeSmartAsync(TBaseArg arg, CancellationToken cancellationToken = default)
{
List<Delegate> list = GetDelegates(arg);
if (list.Count == 0)
throw new MissingMethodException($"No delegate is registered for type {arg.GetType()} or its base types.");

return list.Count >= _parallelThreshold
? InvokeParallelAsync(arg, cancellationToken)
: InvokeAsync(arg, cancellationToken);
}

/// <inheritdoc />
public IEnumerator<Delegate> GetEnumerator() => _delegates.Values.SelectMany(d => d).GetEnumerator();

/// <inheritdoc />
IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

