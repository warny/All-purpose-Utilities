using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using Utils.Objects;

namespace Utils.Network;

/// <summary>
/// Represents a contiguous range of IP addresses. Instances are immutable and
/// may be constructed from a pair of addresses or from a single address with a
/// prefix length. The range can be enumerated to list each address it contains.
/// </summary>
public class IpRange : IParsable<IpRange>, IEnumerable<IPAddress>,
    IEquatable<IpRange>, IEqualityOperators<IpRange, IpRange, bool>
{
    /// <summary>
    /// Gets the first address in the range.
    /// </summary>
    public IPAddress Start { get; }

    /// <summary>
    /// Gets the last address in the range.
    /// </summary>
    public IPAddress End { get; }

    /// <summary>
    /// Gets the CIDR mask length if the range aligns to a prefix boundary;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public int? Mask { get; }

    private int? _hashCode;

    /// <summary>
    /// Gets the cached hash code for this instance.
    /// </summary>
    public int HashCode => _hashCode ??= System.HashCode.Combine(Start, End);

    /// <summary>
    /// The private 10.0.0.0/8 IPv4 range.
    /// </summary>
    public static IpRange Private10 { get; } =
        new IpRange(IPAddress.Parse("10.0.0.0"), 8);

    /// <summary>
    /// The private 172.16.0.0/12 IPv4 range.
    /// </summary>
    public static IpRange Private172 { get; } =
        new IpRange(IPAddress.Parse("172.16.0.0"), 12);

    /// <summary>
    /// The private 192.168.0.0/16 IPv4 range.
    /// </summary>
    public static IpRange Private192 { get; } =
        new IpRange(IPAddress.Parse("192.168.0.0"), 16);

    /// <summary>
    /// The IPv4 loopback range 127.0.0.0/8.
    /// </summary>
    public static IpRange Loopback { get; } =
        new IpRange(IPAddress.Parse("127.0.0.0"), 8);

    /// <summary>
    /// The IPv4 link-local range 169.254.0.0/16.
    /// </summary>
    public static IpRange LinkLocal { get; } =
        new IpRange(IPAddress.Parse("169.254.0.0"), 16);

    /// <summary>
    /// The carrier grade NAT range 100.64.0.0/10.
    /// </summary>
    public static IpRange CarrierGradeNat { get; } =
        new IpRange(IPAddress.Parse("100.64.0.0"), 10);

    /// <summary>
    /// The IPv6 loopback address ::1.
    /// </summary>
    public static IpRange IPv6Loopback { get; } =
        new IpRange(IPAddress.IPv6Loopback, 128);

    /// <summary>
    /// The IPv6 unique local address range fc00::/7.
    /// </summary>
    public static IpRange IPv6UniqueLocal { get; } =
        new IpRange(IPAddress.Parse("fc00::"), 7);

    /// <summary>
    /// The IPv6 link-local range fe80::/10.
    /// </summary>
    public static IpRange IPv6LinkLocal { get; } =
        new IpRange(IPAddress.Parse("fe80::"), 10);

    /// <summary>
    /// Initializes a new instance using explicit start and end addresses.
    /// The <see cref="Mask"/> property is computed if the range forms a valid
    /// CIDR block.
    /// </summary>
    /// <param name="start">First address in the range.</param>
    /// <param name="end">Last address in the range.</param>
    public IpRange(IPAddress start, IPAddress end)
    {
        start.Arg().MustNotBeNull();
        end.Arg().MustNotBeNull();
        if (start.AddressFamily != end.AddressFamily)
        {
            throw new ArgumentException("Addresses must be of the same family");
        }

        Start = start;
        End = end;
        if (TryGetMaskLength(out var mask))
            Mask = mask;
    }

    /// <summary>
    /// Initializes a new instance from a single address and prefix length.
    /// </summary>
    /// <param name="address">Base address of the block.</param>
    /// <param name="maskLength">Number of bits in the network prefix.</param>
    public IpRange(IPAddress address, int maskLength)
    {
        address.Arg().MustNotBeNull();
        maskLength.ArgMustBeBetween(0, address.GetAddressBytes().Length * 8);

        (Start, End) = CalculateRange(address, maskLength);
        Mask = maskLength;
    }

    /// <summary>
    /// Parses a string representing an IP range. Supported formats are
    /// CIDR notation (<c>192.168.0.0/24</c>) and hyphen separated ranges
    /// (<c>192.168.0.1-192.168.0.10</c>).
    /// </summary>
    /// <param name="value">String to parse.</param>
    /// <returns>The parsed <see cref="IpRange"/>.</returns>
    public static IpRange Parse(string value)
    {
        value.Arg().MustNotBeNull();
        value = value.Trim();
        if (value.Contains('/'))
        {
            var parts = value.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) throw new FormatException("Invalid CIDR notation");
            var address = IPAddress.Parse(parts[0]);
            var maskLength = int.Parse(parts[1]);
            return new IpRange(address, maskLength);
        }
        if (value.Contains('-'))
        {
            var parts = value.Split('-', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) throw new FormatException("Invalid range notation");
            var start = IPAddress.Parse(parts[0]);
            var end = IPAddress.Parse(parts[1]);
            return new IpRange(start, end);
        }
        throw new FormatException("Unrecognized IP range format");
    }

    /// <inheritdoc/>
    public static IpRange Parse(string s, IFormatProvider? provider) => Parse(s);

    /// <inheritdoc/>
    public static bool TryParse(string? s, IFormatProvider? provider, out IpRange result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = default!;
            return false;
        }

        try
        {
            result = Parse(s);
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }

    /// <summary>
    /// Determines the prefix length if <see cref="Start"/> and
    /// <see cref="End"/> represent a valid CIDR block.
    /// </summary>
    /// <param name="maskLength">Resulting prefix length.</param>
    /// <returns><see langword="true"/> if the range can be represented as a
    /// mask; otherwise <see langword="false"/>.</returns>
    private bool TryGetMaskLength(out int maskLength)
    {
        var startBytes = Start.GetAddressBytes();
        var endBytes = End.GetAddressBytes();
        if (startBytes.Length != endBytes.Length)
        {
            maskLength = 0;
            return false;
        }

        var totalBits = startBytes.Length * 8;
        var startValue = new BigInteger(startBytes, isUnsigned: true, isBigEndian: true);
        var endValue = new BigInteger(endBytes, isUnsigned: true, isBigEndian: true);
        var diff = endValue - startValue + BigInteger.One;
        if (diff <= BigInteger.Zero || (diff & (diff - BigInteger.One)) != BigInteger.Zero)
        {
            maskLength = 0;
            return false;
        }

        if ((startValue & (diff - BigInteger.One)) != BigInteger.Zero)
        {
            maskLength = 0;
            return false;
        }

        int hostBits = 0;
        var temp = diff;
        while (temp > BigInteger.One)
        {
            temp >>= 1;
            hostBits++;
        }

        maskLength = totalBits - hostBits;
        return true;
    }

    /// <summary>
    /// Calculates the start and end addresses for an address/prefix pair.
    /// </summary>
    private static (IPAddress start, IPAddress end) CalculateRange(IPAddress address, int maskLength)
    {
        var bytes = address.GetAddressBytes();
        var start = new byte[bytes.Length];
        var end = new byte[bytes.Length];

        int full = maskLength / 8;
        int rem = maskLength % 8;

        for (int i = 0; i < full; i++)
        {
            start[i] = bytes[i];
            end[i] = bytes[i];
        }

        if (full < bytes.Length)
        {
            byte mask = (byte)(0xFF << (8 - rem));
            start[full] = (byte)(bytes[full] & mask);
            end[full] = (byte)(bytes[full] | ~mask);
            for (int i = full + 1; i < bytes.Length; i++)
            {
                start[i] = 0;
                end[i] = 0xFF;
            }
        }

        return (new IPAddress(start), new IPAddress(end));
    }

    #region Equality

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is IpRange other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(IpRange? other)
    {
        if (other is null) return false;
        return Start.Equals(other.Start) && End.Equals(other.End);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode;

    /// <summary>
    /// Equality operator comparing two <see cref="IpRange"/> instances.
    /// </summary>
    public static bool operator ==(IpRange? left, IpRange? right)
        => left?.Equals(right) ?? right is null;

    /// <summary>
    /// Inequality operator comparing two <see cref="IpRange"/> instances.
    /// </summary>
    public static bool operator !=(IpRange? left, IpRange? right)
        => !(left == right);

    #endregion

    /// <summary>
    /// Determines whether the specified <paramref name="address"/> is within
    /// the bounds of this range.
    /// </summary>
    /// <param name="address">The address to test.</param>
    /// <returns><see langword="true"/> if the address lies within the range;
    /// otherwise <see langword="false"/>.</returns>
    public bool Contains(IPAddress address)
    {
        address.Arg().MustNotBeNull();
        if (address.AddressFamily != Start.AddressFamily)
            return false;

        var bytes = address.GetAddressBytes();
        var startBytes = Start.GetAddressBytes();
        var endBytes = End.GetAddressBytes();

        var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var startValue = new BigInteger(startBytes, isUnsigned: true, isBigEndian: true);
        var endValue = new BigInteger(endBytes, isUnsigned: true, isBigEndian: true);

        return value >= startValue && value <= endValue;
    }

    /// <summary>
    /// Enumerates all IP addresses contained within this range.
    /// </summary>
    /// <returns>An enumerator over each <see cref="IPAddress"/> in order.</returns>
    public IEnumerator<IPAddress> GetEnumerator()
    {
        var startBytes = Start.GetAddressBytes();
        var endBytes = End.GetAddressBytes();
        if (startBytes.Length != endBytes.Length)
            yield break;

        var startValue = new BigInteger(startBytes, isUnsigned: true, isBigEndian: true);
        var endValue = new BigInteger(endBytes, isUnsigned: true, isBigEndian: true);
        for (var current = startValue; current <= endValue; current++)
        {
            yield return new IPAddress(ToBytes(current, startBytes.Length));
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static byte[] ToBytes(BigInteger value, int byteCount)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (bytes.Length == byteCount)
            return bytes;

        var result = new byte[byteCount];
        if (bytes.Length > byteCount)
        {
            Array.Copy(bytes, bytes.Length - byteCount, result, 0, byteCount);
        }
        else
        {
            Array.Copy(bytes, 0, result, byteCount - bytes.Length, bytes.Length);
        }
        return result;
    }
}
