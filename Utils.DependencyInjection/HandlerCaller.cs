using System;

namespace Utils.DependencyInjection;

/// <summary>
/// Dispatches messages to their associated handlers.
/// </summary>
[Injectable]
public interface IHandlerCaller : IInjectable
{
        /// <summary>
        /// Validates and handles the provided message using the registered handler for its type.
        /// </summary>
        /// <typeparam name="E">Type of validation error.</typeparam>
        /// <param name="message">Message instance to dispatch.</param>
        /// <param name="error">Validation error returned when the method yields <see langword="false"/>.</param>
        /// <returns><see langword="true"/> when the message has been handled; otherwise, <see langword="false"/>.</returns>
        bool Handle<E>(object message, out E error);
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
        public bool Handle<E>(object message, out E error)
        {
                if (message is null)
                {
                        throw new ArgumentNullException(nameof(message));
                }

                error = default!;
                var messageType = message.GetType();

                var checkType = typeof(ICheck<,>).MakeGenericType(messageType, typeof(E));
                var check = this.serviceProvider.GetService(checkType);
                if (check is not null)
                {
                        var checkMethod = checkType.GetMethod("Check");
                        var parameters = new object?[] { message, null };
                        var isValid = (bool)(checkMethod?.Invoke(check, parameters) ?? false);
                        if (!isValid)
                        {
                                if (parameters[1] is E typedError)
                                {
                                        error = typedError;
                                }

                                return false;
                        }
                }

                var handlerType = typeof(IHandler<>).MakeGenericType(messageType);
                var handler = this.serviceProvider.GetService(handlerType);
                if (handler is null)
                {
                        throw new InvalidOperationException($"No handler registered for type {messageType}");
                }

                var handleMethod = handlerType.GetMethod("Handle");
                handleMethod?.Invoke(handler, new[] { message });
                return true;
        }
}

