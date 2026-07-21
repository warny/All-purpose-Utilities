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
    /// 4 MiB is a generous upper bound on the argument and return-value JSON for the integer,
    /// floating-point, and small struct types accepted by <see cref="CrossProcessMarshaling"/>.
    /// This constant replaces the original 64 MiB limit to reduce single-frame allocation cost;
    /// length-prefixed binary framing with configurable per-request budgets is deferred to a
    /// future improvement.
    /// </summary>
    internal const int MaxLineLength = 4 * 1024 * 1024;

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

            sb.Append((char)c);

            if (sb.Length > maxLength)
            {
                throw new InvalidOperationException(
                    $"Protocol framing error: an input line exceeded the maximum allowed length " +
                    $"of {maxLength:N0} characters. This may indicate a DoS attempt or a corrupt connection.");
            }
        }

        // Distinguish "empty line" (c == '\n', sb is empty) from "end of stream" (c == -1, sb is empty).
        return c == -1 && sb.Length == 0 ? null : sb.ToString();
    }
}
