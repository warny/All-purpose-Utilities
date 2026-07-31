using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Utils.IO.Serialization;

/// <summary>Describes the publication state of one runtime serialization contract.</summary>
internal enum ContractBuildState
{
    Building,
    Ready,
    Failed
}

/// <summary>Stores the shared state and completion signal for one contract build.</summary>
internal sealed class ContractCacheEntry
{
    /// <summary>Initializes a building entry owned by the current managed thread.</summary>
    internal ContractCacheEntry(Type contractType)
    {
        ContractType = contractType;
        OwnerId = Environment.CurrentManagedThreadId;
    }

    /// <summary>Gets the contract type.</summary>
    internal Type ContractType { get; }

    /// <summary>Gets or sets the current publication state while the cache gate is held.</summary>
    internal ContractBuildState State { get; set; } = ContractBuildState.Building;

    /// <summary>Gets the managed thread that started construction.</summary>
    internal int OwnerId { get; }

    /// <summary>Gets or sets the published delegate.</summary>
    internal Delegate? CompiledDelegate { get; set; }

    /// <summary>Gets or sets the published failure.</summary>
    internal Exception? Failure { get; set; }
}

/// <summary>
/// Coordinates contract construction with a shared dependency graph so cycles spanning
/// multiple threads are detected before either thread waits for the other.
/// </summary>
internal sealed class ContractCache
{
    private readonly object gate = new();
    private readonly Dictionary<Type, ContractCacheEntry> entries = [];
    private readonly Dictionary<Type, HashSet<Type>> dependencies = [];
    private readonly AsyncLocal<Stack<Type>?> buildStack = new();

    /// <summary>Gets or builds one contract, publishing exactly one result to all callers.</summary>
    internal Delegate GetOrBuild(Type type, Func<Type, Delegate> builder)
    {
        Type? parent = buildStack.Value is { Count: > 0 } stack ? stack.Peek() : null;
        ContractCacheEntry entry;

        lock (gate)
        {
            if (parent is not null)
            {
                AddDependency(parent, type);
                if (TryFindPath(type, parent, out List<Type>? path))
                {
                    RemoveDependency(parent, type);
                    throw CreateCycleException(type, path.Prepend(parent));
                }
            }

            if (!entries.TryGetValue(type, out entry!))
            {
                entry = new ContractCacheEntry(type);
                entries.Add(type, entry);
            }
            else
            {
                while (entry.State == ContractBuildState.Building)
                {
                    Monitor.Wait(gate);
                }

                RemoveDependency(parent, type);
                return GetPublished(entry);
            }
        }

        Stack<Type> currentStack = buildStack.Value ??= new Stack<Type>();
        currentStack.Push(type);
        try
        {
            Delegate result = builder(type);
            PublishSuccess(entry, result);
            return result;
        }
        catch (Exception failure)
        {
            PublishFailure(entry, failure);
            throw;
        }
        finally
        {
            currentStack.Pop();
            if (currentStack.Count == 0) buildStack.Value = null;
            lock (gate) RemoveDependency(parent, type);
        }
    }

    /// <summary>Adds one edge to the shared in-progress dependency graph.</summary>
    private void AddDependency(Type source, Type target)
    {
        if (!dependencies.TryGetValue(source, out HashSet<Type>? targets))
        {
            targets = [];
            dependencies.Add(source, targets);
        }
        targets.Add(target);
    }

    /// <summary>Removes a completed or rejected dependency edge.</summary>
    private void RemoveDependency(Type? source, Type target)
    {
        if (source is null || !dependencies.TryGetValue(source, out HashSet<Type>? targets)) return;
        targets.Remove(target);
        if (targets.Count == 0) dependencies.Remove(source);
    }

    /// <summary>Finds a graph path using deterministic depth-first traversal.</summary>
    private bool TryFindPath(Type start, Type target, out List<Type> path)
    {
        path = [];
        return Visit(start, target, [], path);
    }

    /// <summary>Visits the shared dependency graph without revisiting nodes.</summary>
    private bool Visit(Type current, Type target, HashSet<Type> visited, List<Type> path)
    {
        if (!visited.Add(current)) return false;
        path.Add(current);
        if (current == target) return true;
        if (dependencies.TryGetValue(current, out HashSet<Type>? next))
        {
            foreach (Type candidate in next.OrderBy(GetStableName, StringComparer.Ordinal))
            {
                if (Visit(candidate, target, visited, path)) return true;
            }
        }
        path.RemoveAt(path.Count - 1);
        return false;
    }

    /// <summary>Publishes a successful build and wakes every waiter.</summary>
    private void PublishSuccess(ContractCacheEntry entry, Delegate result)
    {
        lock (gate)
        {
            entry.CompiledDelegate = result;
            entry.State = ContractBuildState.Ready;
            dependencies.Remove(entry.ContractType);
            Monitor.PulseAll(gate);
        }
    }

    /// <summary>Publishes the original failure instance and wakes every waiter.</summary>
    private void PublishFailure(ContractCacheEntry entry, Exception failure)
    {
        lock (gate)
        {
            entry.Failure = failure;
            entry.State = ContractBuildState.Failed;
            dependencies.Remove(entry.ContractType);
            Monitor.PulseAll(gate);
        }
    }

    /// <summary>Returns a completed delegate or rethrows its shared structured failure.</summary>
    private static Delegate GetPublished(ContractCacheEntry entry) => entry.State switch
    {
        ContractBuildState.Ready => entry.CompiledDelegate!,
        ContractBuildState.Failed => throw entry.Failure!,
        _ => throw new InvalidOperationException("A building contract cannot be published.")
    };

    /// <summary>Creates a structured cycle diagnostic containing stable full type names.</summary>
    private static SerializationContractException CreateCycleException(Type contractType, IEnumerable<Type> path)
    {
        string cycle = string.Join(" -> ", path.Select(GetStableName));
        return new SerializationContractException(contractType,
            [new SerializationContractDiagnostic("UIORT007", $"Recursive serialization contract detected: {cycle}.")]);
    }

    /// <summary>Gets the stable diagnostic identity for a runtime type.</summary>
    private static string GetStableName(Type type) => type.FullName ?? type.Name;
}
