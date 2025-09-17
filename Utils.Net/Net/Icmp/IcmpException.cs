using System;

namespace Utils.Net.Icmp;

/// <summary>
/// Represents an error that occurs while sending or receiving ICMP packets.
/// </summary>
public class IcmpException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IcmpException"/> class with the specified message.
    /// </summary>
    /// <param name="message">A description of the failure.</param>
    public IcmpException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IcmpException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">A description of the failure.</param>
    /// <param name="innerException">The exception that triggered the ICMP failure.</param>
    public IcmpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
