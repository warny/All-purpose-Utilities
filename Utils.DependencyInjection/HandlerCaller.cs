using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Utils.DependencyInjection;

/// <summary>
/// Dispatches messages to their associated handlers.
/// </summary>
[Injectable]
public interface IHandlerCaller : IInjectable
{
    /// <summary>
    /// Validates and handles the provided message using the registered handlers for its type.
    /// </summary>
    /// <typeparam name="E">Type of validation error.</typeparam>
    /// <param name="message">Message instance to dispatch.</param>
    /// <param name="errors">Collection receiving validation errors when the method yields <see langword="false"/>. Each entry also contains the source check type.</param>
    /// <returns><see langword="true"/> when the message has been handled; otherwise, <see langword="false"/>.</returns>
    bool Handle<E>(object message, List<CheckError<E>> errors);
}

/// <summary>
/// Default implementation of <see cref="IHandlerCaller"/> using <see cref="IServiceProvider"/>.
/// </summary>
[Singleton]
public class HandlerCaller : IHandlerCaller
{
    // Cached per (message runtime type, error type E) — types and their methods never change.
    private static readonly ConcurrentDictionary<(Type MessageType, Type ErrorType), ReflectionEntry> _cache = new();

    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerCaller"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve handlers.</param>
    public HandlerCaller(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public bool Handle<E>(object message, List<CheckError<E>> errors)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(errors);

        errors.Clear();
        var messageType = message.GetType();

        var entry = _cache.GetOrAdd((messageType, typeof(E)), static key =>
        {
            var (msgType, errType) = key;
            var checkType = typeof(ICheck<,>).MakeGenericType(msgType, errType);
            var handlerType = typeof(IHandler<>).MakeGenericType(msgType);
            return new ReflectionEntry(
                ChecksType: typeof(IEnumerable<>).MakeGenericType(checkType),
                CheckMethod: checkType.GetMethod("Check"),
                HandlersType: typeof(IEnumerable<>).MakeGenericType(handlerType),
                HandleMethod: handlerType.GetMethod("Handle")
            );
        });

        var checks = (IEnumerable<object>?)this.serviceProvider.GetService(entry.ChecksType) ?? [];
        var isValid = true;
        foreach (var check in checks)
        {
            var checkErrors = new List<E>();
            object?[] parameters = [message, checkErrors];
            if (!(bool)(entry.CheckMethod?.Invoke(check, parameters) ?? false))
            {
                foreach (var error in checkErrors)
                    errors.Add(new CheckError<E>(check.GetType(), error));
                isValid = false;
            }
        }
        if (!isValid) return false;

        var handlers = (IEnumerable<object>?)this.serviceProvider.GetService(entry.HandlersType) ?? [];
        var invoked = false;
        foreach (var handler in handlers)
        {
            entry.HandleMethod?.Invoke(handler, [message]);
            invoked = true;
        }
        if (!invoked)
            throw new InvalidOperationException($"No handler registered for type {messageType}");

        return true;
    }

    private sealed record ReflectionEntry(
        Type ChecksType,
        MethodInfo? CheckMethod,
        Type HandlersType,
        MethodInfo? HandleMethod);
}

