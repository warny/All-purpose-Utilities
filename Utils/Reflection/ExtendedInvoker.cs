using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
    /// <remarks>
    /// <para>
    /// <b>Null arguments</b> are treated as <c>typeof(object)</c> for overload resolution. Delegates
    /// that accept a more specific reference type (e.g. <c>Func&lt;string, TResult&gt;</c>) are not
    /// reachable when the corresponding argument is <see langword="null"/>; register a
    /// <c>Func&lt;object, TResult&gt;</c> overload to handle the null case.
    /// </para>
    /// <para>
    /// <b>Ambiguity</b>: when two or more registered delegates match with the same specificity
    /// distance, <see cref="AmbiguousMatchException"/> is thrown rather than silently selecting
    /// the first registration.
    /// </para>
    /// <para>
    /// <b>Exceptions</b>: if the selected delegate throws, the original exception is rethrown
    /// directly (not wrapped in <see cref="System.Reflection.TargetInvocationException"/>).
    /// </para>
    /// </remarks>
    /// <param name="arguments">An array of arguments for the delegate call.</param>
    /// <param name="result">Receives the invocation result if successful.</param>
    /// <returns><see langword="true"/> if a matching delegate was invoked; otherwise <see langword="false"/>.</returns>
    /// <exception cref="AmbiguousMatchException">
    /// Thrown when two or more delegates match the arguments with equal specificity.
    /// </exception>
    public bool TryInvoke(object?[] arguments, out TResult result)
    {
        arguments ??= [];
        var argumentTypes = arguments.Select(a => a?.GetType() ?? typeof(object));

        var candidates = _delegates
            .Select(d => new { Delegate = d, Distance = d.Method.CompareParametersAndTypes(null, argumentTypes) })
            .Where(c => c.Distance >= 0)
            .OrderBy(c => c.Distance)
            .ToList();

        if (candidates.Count == 0)
        {
            result = default!;
            return false;
        }

        if (candidates.Count >= 2 && candidates[0].Distance == candidates[1].Distance)
        {
            throw new AmbiguousMatchException(
                "Multiple registered delegates match the provided arguments with equal specificity. " +
                "Use more specific parameter types or remove the ambiguous registration.");
        }

        try
        {
            result = (TResult)candidates[0].Delegate.DynamicInvoke(arguments)!;
            return true;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            result = default!; // unreachable — ExceptionDispatchInfo.Throw() never returns
            return false;
        }
    }

    /// <summary>
    /// Invokes the best matching delegate for the provided arguments or throws if none is found.
    /// </summary>
    /// <param name="arguments">Arguments for the delegate call.</param>
    /// <returns>The result of the invoked delegate.</returns>
    /// <exception cref="MissingMethodException">Thrown when no suitable delegate is registered.</exception>
    /// <exception cref="AmbiguousMatchException">
    /// Thrown when two or more delegates match the arguments with equal specificity.
    /// </exception>
    public TResult Invoke(params object?[] arguments)
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
