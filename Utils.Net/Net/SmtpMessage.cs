using System.Collections.Generic;

namespace Utils.Net;

/// <summary>
/// Represents a message exchanged via SMTP.
/// </summary>
/// <param name="From">Mail sender address.</param>
/// <param name="Recipients">Recipient addresses.</param>
/// <param name="Data">Raw message data.</param>
public sealed record SmtpMessage(string From, IReadOnlyList<string> Recipients, string Data);
