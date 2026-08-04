using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Utils.Net;

/// <summary>
/// Represents a complete, negatively framed response from a text protocol server.
/// </summary>
public class ProtocolResponseException : IOException
{
    /// <summary>Initializes a structured protocol response exception.</summary>
    public ProtocolResponseException(string protocol, string command, IReadOnlyList<ServerResponse> responses, string? enhancedStatusCode = null, Exception? innerException = null)
        : base(CreateMessage(protocol, command, responses), innerException)
    {
        Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        Command = SanitizeCommand(command);
        ServerResponse[] copy = responses?.ToArray() ?? throw new ArgumentNullException(nameof(responses));
        Responses = new ReadOnlyCollection<ServerResponse>(copy);
        ResponseCode = copy.Length == 0 ? null : copy[^1].Code;
        Severity = copy.Length == 0 ? ResponseSeverity.Unknown : copy[^1].Severity;
        EnhancedStatusCode = enhancedStatusCode;
    }

    /// <summary>Gets the protocol name.</summary>
    public string Protocol { get; }
    /// <summary>Gets the command verb without arguments.</summary>
    public string Command { get; }
    /// <summary>Gets the final response code.</summary>
    public string? ResponseCode { get; }
    /// <summary>Gets the final response severity.</summary>
    public ResponseSeverity Severity { get; }
    /// <summary>Gets an immutable snapshot of all response lines.</summary>
    public IReadOnlyList<ServerResponse> Responses { get; }
    /// <summary>Gets an SMTP enhanced status code when one was supplied.</summary>
    public string? EnhancedStatusCode { get; }

    /// <summary>Removes all command arguments, including credentials.</summary>
    private static string SanitizeCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        int separator = command.IndexOf(' ');
        return (separator < 0 ? command : command[..separator]).ToUpperInvariant();
    }

    /// <summary>Creates a stable message that does not disclose command arguments.</summary>
    private static string CreateMessage(string protocol, string command, IReadOnlyList<ServerResponse> responses)
    {
        string verb = SanitizeCommand(command);
        string code = responses is { Count: > 0 } ? responses[^1].Code : "<missing>";
        return $"{protocol} command {verb} failed with response {code}.";
    }
}

/// <summary>Indicates that protocol framing was lost and the connection cannot be reused.</summary>
public sealed class ProtocolSessionPoisonedException : IOException
{
    /// <summary>Initializes a poisoned-session exception.</summary>
    public ProtocolSessionPoisonedException(string message, Exception? innerException = null) : base(message, innerException) { }
}

/// <summary>Validates structured protocol responses.</summary>
internal static class ProtocolResponseValidator
{
    /// <summary>Requires one of the supplied response codes.</summary>
    internal static void RequireCode(string protocol, string command, IReadOnlyList<ServerResponse> responses, params string[] expectedCodes)
    {
        if (responses.Count == 0 || !expectedCodes.Contains(responses[^1].Code, StringComparer.OrdinalIgnoreCase))
            throw new ProtocolResponseException(protocol, command, responses);
    }

    /// <summary>Requires a positive completion response.</summary>
    internal static void RequireCompletion(string protocol, string command, IReadOnlyList<ServerResponse> responses)
    {
        if (responses.Count == 0 || responses[^1].Severity != ResponseSeverity.Completion)
            throw new ProtocolResponseException(protocol, command, responses);
    }

    /// <summary>Requires a positive intermediate response.</summary>
    internal static void RequireIntermediate(string protocol, string command, IReadOnlyList<ServerResponse> responses)
    {
        if (responses.Count == 0 || responses[^1].Severity != ResponseSeverity.Intermediate)
            throw new ProtocolResponseException(protocol, command, responses);
    }
}
