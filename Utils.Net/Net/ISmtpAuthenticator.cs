using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Provides authentication services for the <see cref="SmtpServer"/>.
/// </summary>
public interface ISmtpAuthenticator
{
    /// <summary>
    /// Authenticates the specified user.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="password">User password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication result including relay permission.</returns>
    Task<SmtpAuthenticationResult> AuthenticateAsync(string user, string password, CancellationToken cancellationToken = default);
}
