using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Utils.Reflection;

/// <summary>
/// A utility class that allows dynamic invocation of delegate methods based on the runtime type of an argument.
/// </summary>
/// <remarks>
/// Dispatch order: exact type match first, then base classes from most- to least-derived, then
/// interfaces. Interface matches are ambiguous when two registered interfaces are both implemented
/// by the runtime type without one being a sub-interface of the other; an
/// <see cref="AmbiguousMatchException"/> is thrown in that case.
/// </remarks>
/// <typeparam name="TBaseArg">The base type for the argument of the delegates.</typeparam>
/// <typeparam name="TResult">The return type of the delegates.</typeparam>
public class DelegateInvoker<TBaseArg, TResult> : IEnumerable<Delegate>
{
    // Snapshot updated atomically on Add so reads in TryInvoke never see a torn state.
    private volatile ImmutableDictionary<Type, Delegate> _delegates =
        ImmutableDictionary<Type, Delegate>.Empty;

    private readonly object _writeLock = new();

    /// <summary>
    /// Registers a delegate function that takes a specific argument type derived from TBaseArg and returns TResult.
    /// </summary>
    /// <typeparam name="T">The specific argument type derived from TBaseArg.</typeparam>
    /// <param name="function">The delegate function to register.</param>
    public void Add<T>(Func<T, TResult> function) where T : TBaseArg
    {
        ArgumentNullException.ThrowIfNull(function);
        lock (_writeLock)
        {
            _delegates = _delegates.SetItem(typeof(T), function);
        }
    }

    /// <summary>
    /// Attempts to invoke the registered delegate for the runtime type of the provided argument.
    /// </summary>
    /// <param name="arg">The argument of type TBaseArg to use for delegate invocation.</param>
    /// <param name="result">The result of the delegate invocation if successful.</param>
    /// <returns>True if a matching delegate is found and invoked, otherwise false.</returns>
    /// <exception cref="System.Reflection.AmbiguousMatchException">
    /// Thrown when two or more registered interfaces match the runtime type without a clear
    /// specificity ordering (neither is a sub-interface of the other).
    /// </exception>
    public bool TryInvoke(TBaseArg arg, out TResult result)
    {
        if (arg == null) throw new ArgumentNullException(nameof(arg));

        ImmutableDictionary<Type, Delegate> snapshot = _delegates;
        Delegate? found = FindDelegate(arg.GetType(), snapshot);

        if (found != null)
        {
            result = (TResult)found.DynamicInvoke(arg)!;
            return true;
        }

        result = default!;
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

    // ─── Private helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the best delegate for <paramref name="runtimeType"/> using the specificity order:
    /// exact type → base classes (most-derived first) → interfaces (most-specific first).
    /// </summary>
    private static Delegate? FindDelegate(Type runtimeType, ImmutableDictionary<Type, Delegate> snapshot)
    {
        // 1. Walk the class hierarchy (exact match first, then base classes).
        for (Type? t = runtimeType; t != null; t = t.BaseType)
        {
            if (snapshot.TryGetValue(t, out Delegate? d)) return d;
        }

        // 2. Check interfaces: collect all candidates, then disambiguate.
        Type[] allInterfaces = runtimeType.GetInterfaces();
        var ifaceMatches = allInterfaces
            .Where(i => snapshot.ContainsKey(i))
            .ToList();

        return ifaceMatches.Count switch
        {
            0 => null,
            1 => snapshot[ifaceMatches[0]],
            _ => ResolveInterfaceAmbiguity(ifaceMatches, snapshot),
        };
    }

    /// <summary>
    /// From two or more matching interfaces, elects the most specific one (a sub-interface of all
    /// others). Throws <see cref="System.Reflection.AmbiguousMatchException"/> when no single winner
    /// exists.
    /// </summary>
    private static Delegate ResolveInterfaceAmbiguity(
        List<Type> candidates, ImmutableDictionary<Type, Delegate> snapshot)
    {
        // A candidate is "more specific" than another if it implements (or is) the other.
        Type? best = null;
        foreach (Type candidate in candidates)
        {
            if (best is null || best.IsAssignableFrom(candidate))
            {
                best = candidate;
            }
            else if (!candidate.IsAssignableFrom(best))
            {
                throw new System.Reflection.AmbiguousMatchException(
                    $"Ambiguous interface match: both '{candidate.FullName}' and '{best.FullName}' " +
                    "are registered and neither is a sub-interface of the other.");
            }
        }

        // Verify the elected winner is indeed more specific than every other candidate.
        foreach (Type candidate in candidates)
        {
            if (candidate != best && !candidate.IsAssignableFrom(best!))
            {
                throw new System.Reflection.AmbiguousMatchException(
                    $"Ambiguous interface match among: {string.Join(", ", candidates.Select(c => c.FullName))}.");
            }
        }

        return snapshot[best!];
    }
}
