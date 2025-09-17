using System.Net;

namespace Utils.Net.Icmp;

/// <summary>
/// Represents a single hop returned by a traceroute operation.
/// </summary>
public class TracerouteHop
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TracerouteHop"/> class.
    /// </summary>
    /// <param name="hopNumber">The sequential hop number starting at 1.</param>
    /// <param name="address">The IP address reached for the hop, or <c>null</c> when unreachable.</param>
    /// <param name="roundTripTime">The measured round-trip time in milliseconds.</param>
    /// <param name="isFinalDestination">Indicates whether the hop corresponds to the requested destination.</param>
    /// <param name="isTimeExceed">Indicates whether the hop resulted from a TTL exceeded response.</param>
    public TracerouteHop(int hopNumber, IPAddress? address, int roundTripTime, bool isFinalDestination, bool isTimeExceed)
    {
        HopNumber = hopNumber;
        Address = address;
        RoundTripTime = roundTripTime;
        IsFinalDestination = isFinalDestination;
        IsTimeExceed = isTimeExceed;
    }

    /// <summary>
    /// Gets the sequential hop number reported by the traceroute.
    /// </summary>
    public int HopNumber { get; }

    /// <summary>
    /// Gets the IP address reached for the hop, or <c>null</c> when the hop timed out.
    /// </summary>
    public IPAddress? Address { get; }

    /// <summary>
    /// Gets the measured round-trip time in milliseconds.
    /// </summary>
    public int RoundTripTime { get; }

    /// <summary>
    /// Gets a value indicating whether the hop corresponds to the destination host.
    /// </summary>
    public bool IsFinalDestination { get; }

    /// <summary>
    /// Gets a value indicating whether the hop was reported through a TTL exceeded response.
    /// </summary>
    public bool IsTimeExceed { get; }

    /// <summary>
    /// Returns a string that represents the current hop.
    /// </summary>
    /// <returns>A string containing the hop number, address, latency, and destination status.</returns>
    public override string ToString()
    {
        string addressText = Address?.ToString() ?? "*";
        string destinationSuffix = IsFinalDestination ? " [Destination Reached]" : string.Empty;
        return $"{HopNumber}: {addressText} - {RoundTripTime}ms{destinationSuffix}";
    }
}
