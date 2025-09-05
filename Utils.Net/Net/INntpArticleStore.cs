using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Utils.Net;

/// <summary>
/// Provides access to newsgroup articles for the NNTP server.
/// </summary>
public interface INntpArticleStore
{
    /// <summary>
    /// Retrieves the names of all available newsgroups.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of group names.</returns>
    Task<IReadOnlyCollection<string>> ListGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the creation time of the specified newsgroup in UTC.
    /// </summary>
    /// <param name="group">Name of the newsgroup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Creation time or <see langword="null"/> if the group does not exist.</returns>
    Task<DateTime?> GetGroupCreationDateAsync(string group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all articles available in the specified group.
    /// </summary>
    /// <param name="group">Name of the newsgroup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping article numbers to their text.</returns>
    Task<IReadOnlyDictionary<int, string>> ListAsync(string group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves article numbers newer than the given time from the specified group.
    /// </summary>
    /// <param name="group">Name of the newsgroup.</param>
    /// <param name="sinceUtc">Lower bound in UTC. Articles newer than this time are returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of article numbers.</returns>
    Task<IReadOnlyCollection<int>> ListNewsSinceAsync(string group, DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full text of a single article.
    /// </summary>
    /// <param name="group">Name of the newsgroup.</param>
    /// <param name="id">Article number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Article text or <see langword="null"/> if not found.</returns>
    Task<string?> RetrieveAsync(string group, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new article to the specified group.
    /// </summary>
    /// <param name="group">Name of the newsgroup.</param>
    /// <param name="article">Full article text including headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number assigned to the new article.</returns>
    Task<int> AddAsync(string group, string article, CancellationToken cancellationToken = default);
}

