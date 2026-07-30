namespace Utils.NumberToString;

/// <summary>Specifies how registration handles a culture already present in the global registry.</summary>
public enum DuplicateCulturePolicy
{
    /// <summary>Rejects the complete batch.</summary>
    Reject,
    /// <summary>Keeps the existing converter.</summary>
    KeepExisting,
    /// <summary>Replaces the existing converter.</summary>
    Replace,
}
