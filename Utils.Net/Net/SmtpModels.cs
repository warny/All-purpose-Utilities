using System;
using System.Globalization;
using System.Linq;
using System.Net;

namespace Utils.Net;

/// <summary>Represents a validated SMTP reverse-path or forward-path.</summary>
public sealed record SmtpPath
{
    /// <summary>Initializes a validated SMTP path.</summary>
    private SmtpPath(string value, bool allowsUtf8) { Value = value; AllowsUtf8 = allowsUtf8; }
    /// <summary>Gets the path without surrounding angle brackets.</summary>
    public string Value { get; }
    /// <summary>Gets whether non-ASCII characters were explicitly permitted.</summary>
    public bool AllowsUtf8 { get; }

    /// <summary>Parses an ASCII SMTP path. An empty value is a valid reverse-path.</summary>
    public static SmtpPath Parse(string value) => Parse(value, false);

    /// <summary>Parses an SMTP path with an explicit SMTPUTF8 policy.</summary>
    public static SmtpPath Parse(string value, bool allowUtf8)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!TryParse(value, allowUtf8, out SmtpPath? path))
            throw new FormatException("The value is not a supported SMTP path.");
        return path;
    }

    /// <summary>Attempts to parse an ASCII SMTP path.</summary>
    public static bool TryParse(string value, out SmtpPath? path) => TryParse(value, false, out path);

    /// <summary>Attempts to parse an SMTP path with an explicit SMTPUTF8 policy.</summary>
    public static bool TryParse(string? value, bool allowUtf8, out SmtpPath? path)
    {
        path = null;
        if (value is null) return false;
        if (value.Length == 0) { path = new SmtpPath(value, allowUtf8); return true; }
        if (value.Any(c => c is '<' or '>' or '\r' or '\n' or '\0' || char.IsWhiteSpace(c) || char.IsControl(c))) return false;
        if (!allowUtf8 && value.Any(c => c > 0x7f)) return false;
        if (value.StartsWith('@') || value.Contains(':') && !value.Contains("@[", StringComparison.Ordinal)) return false;
        int at = value.LastIndexOf('@');
        if (at <= 0 || at == value.Length - 1 || value.IndexOf('@') != at) return false;
        string domain = value[(at + 1)..];
        if (domain.StartsWith('['))
        {
            if (!domain.EndsWith(']')) return false;
            string literal = domain[1..^1];
            if (literal.StartsWith("IPv6:", StringComparison.OrdinalIgnoreCase))
            {
                if (!IPAddress.TryParse(literal[5..], out IPAddress? ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return false;
            }
            else if (!IPAddress.TryParse(literal, out IPAddress? ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        }
        else if (domain.StartsWith('.') || domain.EndsWith('.') || domain.Split('.').Any(label => label.Length == 0 || label.StartsWith('-') || label.EndsWith('-') || label.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))) return false;
        path = new SmtpPath(value, allowUtf8);
        return true;
    }
}

/// <summary>Identifies the SMTP BODY parameter.</summary>
public enum SmtpBodyKind { /// <summary>Seven-bit content.</summary>
    SevenBit, /// <summary>Eight-bit MIME content.</summary>
    EightBitMime }

/// <summary>Defines validated ESMTP MAIL parameters separately from a mailbox path.</summary>
public sealed record SmtpMailOptions
{
    private long? _size;
    /// <summary>Gets or initializes the declared message size.</summary>
    public long? Size { get => _size; init => _size = value < 0 ? throw new ArgumentOutOfRangeException(nameof(Size)) : value; }
    /// <summary>Gets or initializes the BODY parameter.</summary>
    public SmtpBodyKind? Body { get; init; }
    /// <summary>Gets or initializes whether SMTPUTF8 is explicitly enabled.</summary>
    public bool SmtpUtf8 { get; init; }
}

/// <summary>Contains the three independently validated SASL PLAIN credential fields.</summary>
public sealed record SmtpPlainCredentials
{
    /// <summary>Initializes credentials and rejects embedded NUL delimiters.</summary>
    public SmtpPlainCredentials(string authenticationIdentity, string password, string authorizationIdentity = "")
    {
        AuthenticationIdentity = Validate(authenticationIdentity, nameof(authenticationIdentity), false);
        Password = Validate(password, nameof(password), true);
        AuthorizationIdentity = Validate(authorizationIdentity, nameof(authorizationIdentity), true);
    }
    /// <summary>Gets the authentication identity.</summary>
    public string AuthenticationIdentity { get; }
    /// <summary>Gets the password.</summary>
    public string Password { get; }
    /// <summary>Gets the optional authorization identity.</summary>
    public string AuthorizationIdentity { get; }
    /// <summary>Validates one SASL field.</summary>
    private static string Validate(string value, string name, bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (!allowEmpty && value.Length == 0) throw new ArgumentException("The identity must not be empty.", name);
        if (value.Contains('\0')) throw new ArgumentException("SASL fields must not contain NUL.", name);
        return value;
    }
}
