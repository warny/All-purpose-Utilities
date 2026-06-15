using System;

namespace Utils.VirtualMachine;

/// <summary>
/// Represents errors that occur while executing a <see cref="VirtualProcessor{T}"/>.
/// </summary>
[Serializable]
public class VirtualProcessorException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualProcessorException"/> class.
    /// </summary>
    public VirtualProcessorException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualProcessorException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public VirtualProcessorException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualProcessorException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public VirtualProcessorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

}
