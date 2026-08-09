using System;

namespace Utils.Net;

/// <summary>Defines hard limits for a dot-terminated protocol payload.</summary>
public sealed record ProtocolPayloadLimits
{
    private int _maximumLines = 100_000;
    private long _maximumCharacters = 10 * 1024 * 1024;
    private long _maximumBytes = 40 * 1024 * 1024;

    /// <summary>Gets or initializes the maximum line count; zero disables this limit.</summary>
    public int MaximumLines { get => _maximumLines; init => _maximumLines = Validate(value, nameof(MaximumLines)); }
    /// <summary>Gets or initializes the maximum UTF-16 character count; zero disables this limit.</summary>
    public long MaximumCharacters { get => _maximumCharacters; init => _maximumCharacters = Validate(value, nameof(MaximumCharacters)); }
    /// <summary>Gets or initializes the maximum UTF-8 byte count; zero disables this limit.</summary>
    public long MaximumBytes { get => _maximumBytes; init => _maximumBytes = Validate(value, nameof(MaximumBytes)); }

    /// <summary>Validates a non-negative integer limit.</summary>
    private static int Validate(int value, string name) => value < 0 ? throw new ArgumentOutOfRangeException(name) : value;
    /// <summary>Validates a non-negative long limit.</summary>
    private static long Validate(long value, string name) => value < 0 ? throw new ArgumentOutOfRangeException(name) : value;
}
