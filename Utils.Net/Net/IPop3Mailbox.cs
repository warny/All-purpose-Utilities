using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Defines operations required by a POP3 server to access mailbox data.
/// </summary>
public interface IPop3Mailbox
{
    /// <summary>
    /// Authenticates the specified user.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="password">Password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if authentication succeeds; otherwise, <see langword="false"/>.</returns>
    Task<bool> AuthenticateAsync(string user, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user using the APOP challenge-response mechanism.
    /// </summary>
    /// <param name="user">User name.</param>
    /// <param name="timestamp">Timestamp provided in the server greeting.</param>
    /// <param name="digest">MD5 digest of <paramref name="timestamp"/> and the user password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if authentication succeeds; otherwise, <see langword="false"/>.</returns>
    Task<bool> AuthenticateApopAsync(string user, string timestamp, string digest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of available messages with their sizes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping message identifiers to their size in bytes.</returns>
    Task<IReadOnlyDictionary<int, int>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full text of a message.
    /// </summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Message text.</returns>
    Task<string> RetrieveAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of available messages with their unique identifiers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping message identifiers to unique identifiers.</returns>
    Task<IReadOnlyDictionary<int, string>> ListUidsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified message.
    /// </summary>
    /// <param name="id">Message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
