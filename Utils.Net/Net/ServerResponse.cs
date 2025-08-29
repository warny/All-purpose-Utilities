using Utils.Objects;

namespace Utils.Net;

/// <summary>
/// Indicates the severity of a server response.
/// </summary>
public enum ResponseSeverity
{
    /// <summary>
    /// Severity could not be determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Positive preliminary reply. Further responses will follow.
    /// </summary>
    Preliminary = 1,

    /// <summary>
    /// Positive completion reply.
    /// </summary>
    Completion = 2,

    /// <summary>
    /// Positive intermediate reply requiring more information.
    /// </summary>
    Intermediate = 3,

    /// <summary>
    /// Transient negative completion reply.
    /// </summary>
    TransientNegative = 4,

    /// <summary>
    /// Permanent negative completion reply.
    /// </summary>
    PermanentNegative = 5
}

/// <summary>
/// Represents a single server response line consisting of a numeric code, its severity and an optional message.
/// </summary>
public readonly record struct ServerResponse
{
    /// <summary>
    /// Numeric status code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Severity of the response.
    /// </summary>
    public ResponseSeverity Severity { get; }

    /// <summary>
    /// Optional associated message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerResponse"/> struct.
    /// </summary>
    /// <param name="code">Numeric status code.</param>
    /// <param name="severity">Severity of the response.</param>
    /// <param name="message">Optional associated message.</param>
    public ServerResponse(string code, ResponseSeverity severity, string? message)
    {
        Code = code;
        Severity = severity;
        Message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerResponse"/> struct inferring severity from the status code.
    /// </summary>
    /// <param name="code">Numeric status code.</param>
    /// <param name="message">Optional associated message.</param>
    public ServerResponse(string code, string? message)
        : this(code, (ResponseSeverity)(code[0] - '0'), message)
    {
        code[0].ArgMustBeBetween('0', '5', nameof(code));
    }
}
