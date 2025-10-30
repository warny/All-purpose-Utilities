namespace Utils.DependencyInjection;

/// <summary>
/// Defines a message handler for a specific message type.
/// </summary>
/// <typeparam name="T">Type of message processed by the handler.</typeparam>
[Injectable]
public interface IHandler<in T> : IInjectable
{
    /// <summary>
    /// Processes the provided message instance.
    /// </summary>
    /// <param name="message">Message instance to handle.</param>
    void Handle(T message);
}

