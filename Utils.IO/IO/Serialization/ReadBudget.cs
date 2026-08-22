using System.IO;

namespace Utils.IO.Serialization;

/// <summary>Tracks wire bytes consumed by all readers in one logical parsing operation.</summary>
internal sealed class ReadBudget
{
    private readonly long? maximum;
    private long consumed;

    /// <summary>Initializes a shared budget from an optional maximum.</summary>
    internal ReadBudget(long? maximum) => this.maximum = maximum;

    /// <summary>Rejects a read before allocation when its requested size cannot fit.</summary>
    internal void EnsureAvailable(long count)
    {
        if (count < 0) throw new InvalidDataException("A negative wire byte count is invalid.");
        if (maximum is long limit && count > limit - consumed)
            throw new InvalidDataException($"Reading {count} wire bytes would exceed the configured aggregate limit of {limit} bytes.");
    }

    /// <summary>Records bytes actually returned by the underlying stream.</summary>
    internal void Consume(long count)
    {
        EnsureAvailable(count);
        consumed += count;
    }
}
