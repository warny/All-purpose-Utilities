using System;
using System.IO;
using System.Text;

namespace Utils.Reflection.Reflection.Emit;

/// <summary>
/// Bounded I/O helpers shared between <see cref="EmitWorkerHost"/> (worker side) and
/// <see cref="EmitWorkerProcess"/> (host side).
/// </summary>
internal static class ProtocolFraming
{
    /// <summary>
    /// Maximum number of characters allowed in a single protocol line (request or response JSON).
    /// 64 MiB is retained to avoid a compatibility regression: callers that pass large arrays
    /// (e.g. a multi-megabyte <c>byte[]</c>) would be silently broken by a lower limit because
    /// a 1 MiB binary array encodes to roughly 3 MiB of JSON. Reducing this limit safely requires
    /// length-prefixed binary framing so the receiver can reject oversized frames before allocating,
    /// which is a separate protocol change tracked independently of this audit.
    /// </summary>
    internal const int MaxLineLength = 64 * 1024 * 1024;

    /// <summary>
    /// Reads one line from <paramref name="reader"/>, returning <see langword="null"/> at
    /// end-of-stream. Throws <see cref="InvalidOperationException"/> if the line (before the
    /// newline character) exceeds <paramref name="maxLength"/> characters, so neither end of the
    /// protocol needs to materialize an arbitrarily large string before detecting the violation.
    /// </summary>
    /// <exception cref="InvalidOperationException">The line exceeded <paramref name="maxLength"/>.</exception>
    internal static string? ReadBoundedLine(TextReader reader, int maxLength = MaxLineLength)
    {
        var sb = new StringBuilder();
        int c;
        while ((c = reader.Read()) != -1)
        {
            if (c == '\n')
                break;

            if (c == '\r')
                continue;

            // Check before appending so the StringBuilder never holds more than maxLength chars,
            // avoiding a single off-by-one allocation that would double peak memory at the boundary.
            if (sb.Length == maxLength)
            {
                throw new InvalidOperationException(
                    $"Protocol framing error: an input line exceeded the maximum allowed length " +
                    $"of {maxLength:N0} characters. This may indicate a DoS attempt or a corrupt connection.");
            }

            sb.Append((char)c);
        }

        // Distinguish "empty line" (c == '\n', sb is empty) from "end of stream" (c == -1, sb is empty).
        return c == -1 && sb.Length == 0 ? null : sb.ToString();
    }
}
