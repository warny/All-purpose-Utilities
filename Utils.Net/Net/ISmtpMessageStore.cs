using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Defines the interface used by <see cref="SmtpServer"/> to exchange messages.
/// </summary>
public interface ISmtpMessageStore
{
    /// <summary>
    /// Stores a message received by the server.
    /// </summary>
    /// <param name="message">Message to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreAsync(SmtpMessage message, CancellationToken cancellationToken = default);
}
