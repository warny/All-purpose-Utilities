namespace Utils.Net;

/// <summary>
/// Specifies the authentication mechanism used with the <see cref="SmtpClient"/> and <see cref="SmtpServer"/>.
/// </summary>
public enum SmtpAuthenticationMechanism
{
    /// <summary>
    /// Plain text authentication using a single base64 encoded "user\0user\0password" value.
    /// </summary>
    Plain,

    /// <summary>
    /// Login authentication sending the user name and password as separate base64 encoded values.
    /// </summary>
    Login
}
