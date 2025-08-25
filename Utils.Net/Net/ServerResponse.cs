namespace Utils.Net;

/// <summary>
/// Represents a single server response line consisting of a numeric code and optional message.
/// </summary>
/// <param name="Code">Numeric status code.</param>
/// <param name="Message">Optional associated message.</param>
public readonly record struct ServerResponse(int Code, string? Message);
