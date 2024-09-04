using System;
using System.Collections;
using System.Collections.Generic;

namespace Utils.Reflection;

/// <summary>
/// A utility class that allows dynamic invocation of delegate methods based on the runtime type of an argument.
/// </summary>
/// <typeparam name="TBaseArg">The base type for the argument of the delegates.</typeparam>
/// <typeparam name="TResult">The return type of the delegates.</typeparam>
public class DelegateInvoker<TBaseArg, TResult> : IEnumerable<Delegate>
{
	// Stores delegates associated with specific argument types.
	private readonly Dictionary<Type, Delegate> _delegates = new Dictionary<Type, Delegate>();

	/// <summary>
	/// Registers a delegate function that takes a specific argument type derived from TBaseArg and returns TResult.
	/// </summary>
	/// <typeparam name="T">The specific argument type derived from TBaseArg.</typeparam>
	/// <param name="function">The delegate function to register.</param>
	public void Add<T>(Func<T, TResult> function) where T : TBaseArg
	{
		_delegates[typeof(T)] = function ?? throw new ArgumentNullException(nameof(function));
	}

	/// <summary>
	/// Attempts to invoke the registered delegate for the runtime type of the provided argument.
	/// </summary>
	/// <param name="arg">The argument of type TBaseArg to use for delegate invocation.</param>
	/// <param name="result">The result of the delegate invocation if successful.</param>
	/// <returns>True if a matching delegate is found and invoked, otherwise false.</returns>
	public bool TryInvoke(TBaseArg arg, out TResult result)
	{
		if (arg == null) throw new ArgumentNullException(nameof(arg));

		Type argType = arg.GetType();
		Delegate @delegate = null;

		// Traverse up the inheritance hierarchy to find a matching delegate.
		while (argType != null && !_delegates.TryGetValue(argType, out @delegate))
		{
			argType = argType.BaseType;
		}

		// If a matching delegate is found, invoke it.
		if (@delegate != null)
		{
			result = (TResult)@delegate.DynamicInvoke(arg);
			return true;
		}

		result = default;
		return false;
	}

	/// <summary>
	/// Invokes the delegate registered for the runtime type of the provided argument.
	/// Throws an exception if no matching delegate is found.
	/// </summary>
	/// <param name="arg">The argument of type TBaseArg to use for delegate invocation.</param>
	/// <returns>The result of the delegate invocation.</returns>
	/// <exception cref="MissingMethodException">Thrown when no delegate is registered for the runtime type of the argument.</exception>
	public TResult Invoke(TBaseArg arg)
	{
		if (TryInvoke(arg, out var result))
		{
			return result;
		}

		throw new MissingMethodException($"No delegate is registered for type {arg.GetType()} or its base types.");
	}

	/// <summary>
	/// Returns an enumerator that iterates through the registered delegates.
	/// </summary>
	/// <returns>An enumerator for the registered delegates.</returns>
	public IEnumerator<Delegate> GetEnumerator() => _delegates.Values.GetEnumerator();

	/// <summary>
	/// Returns a non-generic enumerator that iterates through the registered delegates.
	/// </summary>
	/// <returns>A non-generic enumerator for the registered delegates.</returns>
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
