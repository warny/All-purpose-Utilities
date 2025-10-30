using System;
using System.Collections.Generic;

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
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (errors is null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        errors.Clear();
        var messageType = message.GetType();

        var checkType = typeof(ICheck<,>).MakeGenericType(messageType, typeof(E));
        var checksType = typeof(IEnumerable<>).MakeGenericType(checkType);
        var checks = (IEnumerable<object>?)this.serviceProvider.GetService(checksType) ?? [];
        var checkMethod = checkType.GetMethod("Check");
        var isValid = true;
        foreach (var check in checks)
        {
            var checkErrors = new List<E>();
            object?[] parameters = [message, checkErrors];
            if (!(bool)(checkMethod?.Invoke(check, parameters) ?? false))
            {
                foreach (var error in checkErrors)
                {
                    errors.Add(new CheckError<E>(check.GetType(), error));
                }
                isValid = false;
            }
        }
        if (!isValid)
        {
            return false;
        }

        var handlerType = typeof(IHandler<>).MakeGenericType(messageType);
        var handlerCollectionType = typeof(IEnumerable<>).MakeGenericType(handlerType);
        var handlers = (IEnumerable<object>?)this.serviceProvider.GetService(handlerCollectionType) ?? [];
        var handleMethod = handlerType.GetMethod("Handle");
        var invoked = false;
        foreach (var handler in handlers)
        {
            handleMethod?.Invoke(handler, [message]);
            invoked = true;
        }
        if (!invoked)
        {
            throw new InvalidOperationException($"No handler registered for type {messageType}");
        }

        return true;
    }
}

