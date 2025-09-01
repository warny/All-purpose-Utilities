namespace Utils.Net;

/// <summary>
/// Represents the result of an SMTP authentication attempt.
/// </summary>
public sealed class SmtpAuthenticationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpAuthenticationResult"/> class.
    /// </summary>
    /// <param name="isAuthenticated">Indicates whether authentication succeeded.</param>
    /// <param name="canRelay">Indicates whether the authenticated user can relay messages to non-local domains.</param>
    public SmtpAuthenticationResult(bool isAuthenticated, bool canRelay)
    {
        IsAuthenticated = isAuthenticated;
        CanRelay = canRelay;
    }

    /// <summary>
    /// Gets a value indicating whether authentication succeeded.
    /// </summary>
    public bool IsAuthenticated { get; }

    /// <summary>
    /// Gets a value indicating whether the authenticated user is allowed to relay messages to non-local domains.
    /// </summary>
    public bool CanRelay { get; }
}
