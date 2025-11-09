using Utils.Objects;

namespace Utils.OData;

/// <summary>
/// Represents the result of an OData operation, capturing either a value or an <see cref="ErrorReturnValue"/>.
/// </summary>
/// <typeparam name="T">Type of the value returned when the operation succeeds.</typeparam>
public class ReturnValue<T> : Objects.ReturnValue<T, ErrorReturnValue>
{
    /// <summary>
    /// Initializes a new successful instance of the <see cref="ReturnValue{T}"/> class.
    /// </summary>
    /// <param name="value">The value produced by the operation.</param>
    public ReturnValue(T value)
        : base(value)
    {
    }

    /// <summary>
    /// Initializes a new failed instance of the <see cref="ReturnValue{T}"/> class using an error code and message.
    /// </summary>
    /// <param name="code">The error code returned by the operation.</param>
    /// <param name="message">Human-readable description of the failure.</param>
    public ReturnValue(int code, string message)
        : base(new ErrorReturnValue(code, message))
    {
    }

    /// <summary>
    /// Initializes a new failed instance of the <see cref="ReturnValue{T}"/> class using an existing error object.
    /// </summary>
    /// <param name="error">Error descriptor returned by the operation.</param>
    public ReturnValue(ErrorReturnValue error)
        : base(error)
    {
    }

    /// <summary>
    /// Creates a successful <see cref="ReturnValue{T}"/> from a value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public static implicit operator ReturnValue<T>(T value) => new(value);
}

/// <summary>
/// Represents an error returned by an OData operation.
/// </summary>
/// <param name="code">Application-specific or HTTP status code describing the failure.</param>
/// <param name="message">Human-readable explanation of the error.</param>
public record ErrorReturnValue(int code, string message);