using System.IO;

namespace Utils.IO.Serialization;

/// <summary>Tracks wire bytes consumed by all readers in one logical parsing operation.</summary>
internal sealed class ReadBudget
{
    private readonly long? maximum;
    private long consumed;

    /// <summary>Initializes a shared budget from an optional maximum.</summary>
    internal ReadBudget(long? maximum) => this.maximum = maximum;

    /// <summary>Gets whether the requested byte count still fits without changing the budget.</summary>
    internal bool CanConsume(long count)
    {
        if (count < 0) throw new InvalidDataException("A negative wire byte count is invalid.");
        return maximum is not long limit || count <= limit - consumed;
    }

    /// <summary>Rejects a read before allocation when its requested size cannot fit.</summary>
    internal void EnsureAvailable(long count)
    {
        if (count < 0) throw new InvalidDataException("A negative wire byte count is invalid.");
        if (!CanConsume(count))
            throw new InvalidDataException($"Reading {count} wire bytes would exceed the configured aggregate limit of {maximum} bytes.");
    }

    /// <summary>Records bytes actually returned by the underlying stream.</summary>
    internal void Consume(long count)
    {
        EnsureAvailable(count);
        consumed += count;
    }

    /// <summary>
    /// Rejects a single-byte read once the budget is exhausted, without ever performing a physical read
    /// solely to distinguish EOF from a real byte. EOF is reported only when it can be determined from
    /// stream metadata alone.
    /// </summary>
    /// <param name="stream">The stream against which EOF may be observed without reading.</param>
    /// <returns><c>-1</c> when a seekable stream is already positioned at its end.</returns>
    internal int RejectExhausted(Stream stream)
    {
        if (stream.CanSeek && stream.Position == stream.Length) return -1;
        throw new InvalidDataException("Reading another wire byte would exceed the configured aggregate limit.");
    }
}
