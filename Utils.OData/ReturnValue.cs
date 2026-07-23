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
/// Categorizes the origin of an <see cref="ErrorReturnValue"/> so callers can reliably distinguish
/// transport, protocol, deserialization, validation, cancellation, and application errors
/// without inspecting the numeric <see cref="ErrorReturnValue.code"/> (item 31).
/// </summary>
public enum ODataErrorKind
{
    /// <summary>The error category is unspecified. This is the default for legacy callers.</summary>
    Unspecified = 0,

    /// <summary>An HTTP transport failure (non-success status code, no response, unreadable body).</summary>
    Transport = 1,

    /// <summary>An OData protocol failure, such as an invalid continuation link or malformed envelope.</summary>
    Protocol = 2,

    /// <summary>A metadata acquisition or parsing failure.</summary>
    Metadata = 3,

    /// <summary>A payload deserialization or value-conversion failure.</summary>
    Deserialization = 4,

    /// <summary>An input validation failure detected before or during request construction.</summary>
    Validation = 5,

    /// <summary>The operation was cancelled.</summary>
    Cancellation = 6,

    /// <summary>An application-defined error surfaced by higher-level callers.</summary>
    Application = 7
}

/// <summary>
/// Represents an error returned by an OData operation.
/// </summary>
public sealed record ErrorReturnValue
{
    /// <summary>
    /// Initializes a new <see cref="ErrorReturnValue"/> with the given code and message.
    /// </summary>
    /// <param name="code">Application-specific or HTTP status code describing the failure.</param>
    /// <param name="message">Human-readable explanation of the error. Must not be null or empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is null, empty, or whitespace.</exception>
    public ErrorReturnValue(int code, string message)
        : this(code, message, ODataErrorKind.Unspecified, null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="ErrorReturnValue"/> with a code, message, error category, and optional HTTP status.
    /// </summary>
    /// <param name="code">Application-specific or HTTP status code describing the failure.</param>
    /// <param name="message">Human-readable explanation of the error. Must not be null, empty, or whitespace.</param>
    /// <param name="kind">The category of the error (item 31).</param>
    /// <param name="httpStatusCode">The HTTP status code associated with the failure, when applicable.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is null, empty, or whitespace.</exception>
    public ErrorReturnValue(int code, string message, ODataErrorKind kind, int? httpStatusCode = null)
    {
        // Item 32: reject whitespace-only messages, not just null/empty, so that no meaningless
        // error object can be propagated as if it were diagnostic.
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Error message must not be null, empty, or whitespace.", nameof(message));
        }

        this.code = code;
        this.message = message;
        Kind = kind;
        HttpStatusCode = httpStatusCode;
    }

    /// <summary>Application-specific or HTTP status code describing the failure.</summary>
    public int code { get; init; }

    /// <summary>Human-readable explanation of the error.</summary>
    public string message { get; init; }

    /// <summary>
    /// Gets the category of the error, enabling callers to branch on the failure origin without
    /// interpreting the numeric <see cref="code"/> (item 31).
    /// </summary>
    public ODataErrorKind Kind { get; init; }

    /// <summary>
    /// Gets the HTTP status code associated with the failure when the error originates from an
    /// HTTP response; otherwise <see langword="null"/> (item 31).
    /// </summary>
    public int? HttpStatusCode { get; init; }
}
