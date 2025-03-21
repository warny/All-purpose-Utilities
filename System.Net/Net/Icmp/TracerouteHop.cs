using System.Net;

namespace Utils.Net.Icmp;

/// <summary>
/// Represents a single hop in a Traceroute operation.
/// </summary>
public class TracerouteHop
{
	public TracerouteHop(int hopNumber, IPAddress? address, int roundTripTime, bool isFinalDestination, bool isTimeExceed)
	{
		HopNumber = hopNumber;
		Address = address;
		RoundTripTime = roundTripTime;
		IsFinalDestination = isFinalDestination;
		IsTimeExceed = isTimeExceed;
	}

	public int HopNumber { get; }
	public IPAddress? Address { get; }
	public int RoundTripTime { get; } // In milliseconds
	public bool IsFinalDestination { get; }
	public bool IsTimeExceed { get; }

	public override string ToString() =>
		$"{HopNumber}: {(Address != null ? Address.ToString() : "*")} - {RoundTripTime}ms {(IsFinalDestination ? "[Destination Reached]" : "")}";
}