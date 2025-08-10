namespace Utils.DependencyInjection;

/// <summary>
/// Validates messages before they are handled.
/// </summary>
/// <typeparam name="T">Type of message to validate.</typeparam>
/// <typeparam name="E">Type of validation error returned when validation fails.</typeparam>
[Injectable]
public interface ICheck<in T, E> : IInjectable
{
        /// <summary>
        /// Validates the specified message instance.
        /// </summary>
        /// <param name="message">Message instance to validate.</param>
        /// <param name="error">Validation error returned when the method yields <see langword="false"/>.</param>
        /// <returns><see langword="true"/> when the message is valid; otherwise, <see langword="false"/>.</returns>
        bool Check(T message, out E error);
}
