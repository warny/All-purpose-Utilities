using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Utils.Expressions;

namespace Utils.Reflection;

/// <summary>
/// An invoker that can store multiple delegates sharing the same return type
/// but with different parameter lists. On invocation, the best matching
/// delegate is selected using the compiler's distance-based method search.
/// </summary>
/// <typeparam name="TResult">The common return type of all registered delegates.</typeparam>
public class ExtendedInvoker<TResult> : IEnumerable<Delegate>
{
    private readonly List<Delegate> _delegates = new();

    /// <summary>
    /// Adds a delegate to the invoker. The delegate must return
    /// <typeparamref name="TResult"/>.
    /// </summary>
    /// <param name="function">The delegate to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="function"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the delegate return type does not match <typeparamref name="TResult"/>.</exception>
    public void Add(Delegate function)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (function.Method.ReturnType != typeof(TResult))
            throw new ArgumentException($"Delegate must return {typeof(TResult)}", nameof(function));
        _delegates.Add(function);
    }

    /// <summary>
    /// Attempts to invoke the best matching delegate with the provided arguments.
    /// </summary>
    /// <param name="arguments">An array of arguments for the delegate call.</param>
    /// <param name="result">Receives the invocation result if successful.</param>
    /// <returns><see langword="true"/> if a matching delegate was invoked; otherwise <see langword="false"/>.</returns>
    public bool TryInvoke(object[] arguments, out TResult result)
    {
        arguments ??= [];
        var argumentTypes = arguments.Select(a => a?.GetType() ?? typeof(object));

        var candidate = _delegates
                .Select(d => new { Delegate = d, Distance = d.Method.CompareParametersAndTypes(null, argumentTypes) })
                .Where(c => c.Distance >= 0)
                .OrderBy(c => c.Distance)
                .FirstOrDefault();

        if (candidate != null)
        {
            result = (TResult)candidate.Delegate.DynamicInvoke(arguments);
            return true;
        }

        result = default!;
        return false;
    }

    /// <summary>
    /// Invokes the best matching delegate for the provided arguments or throws if none is found.
    /// </summary>
    /// <param name="arguments">Arguments for the delegate call.</param>
    /// <returns>The result of the invoked delegate.</returns>
    /// <exception cref="MissingMethodException">Thrown when no suitable delegate is registered.</exception>
    public TResult Invoke(params object[] arguments)
    {
        if (TryInvoke(arguments, out var result))
            return result;

        throw new MissingMethodException("No delegate matches the provided arguments.");
    }

    /// <inheritdoc />
    public IEnumerator<Delegate> GetEnumerator() => _delegates.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
