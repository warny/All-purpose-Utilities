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
}
