using System;

namespace Utils.IO.Serialization;

/// <summary>Provides the stable runtime-dispatch bridge used by generated serializers.</summary>
public static class WireCodecExtensions
{
    /// <summary>Reads a configured value, optionally applying member-specific codec and framing types.</summary>
    public static T ReadConfigured<T>(this IReader reader, Type? codecType = null, Type? framingType = null)
    {
        if (reader is not Reader configured) throw new NotSupportedException("Configured wire codecs require a Reader implementation.");
        return configured.ReadConfigured<T>(codecType, framingType);
    }

    /// <summary>Writes a configured value, optionally applying member-specific codec and framing types.</summary>
    public static void WriteConfigured<T>(this IWriter writer, T value, Type? codecType = null, Type? framingType = null)
    {
        if (writer is not Writer configured) throw new NotSupportedException("Configured wire codecs require a Writer implementation.");
        configured.WriteConfigured(value, codecType, framingType);
    }
    /// <summary>Uses an exact configured codec when present, otherwise invokes a generated converter fallback.</summary>
    public static T ReadConfiguredOr<T>(this IReader reader, Func<IReader, T> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return reader is Reader configured && configured.TryReadConfigured<T>(out T value) ? value : fallback(reader);
    }

    /// <summary>Uses an exact configured codec when present, otherwise invokes a generated converter fallback.</summary>
    public static void WriteConfiguredOr<T>(this IWriter writer, T value, Action<IWriter, T> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        if (writer is not Writer configured || !configured.TryWriteConfigured(value)) fallback(writer, value);
    }

}
