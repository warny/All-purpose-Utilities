using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using Utils.Net.Icmp;

namespace Utils.Net;

/// <summary>
/// Provides methods for sending and receiving ICMP packets, including Echo Requests and Traceroute.
/// </summary>
public class IcmpUtils
{
	/// <summary>
	/// Sends an ICMP Echo Request (ping) with a random payload and verifies the response.
	/// </summary>
	public static async Task<int> SendEchoRequestAsync(IPAddress destination, int size = 32, int timeout = 1000)
	{
		IcmpPacket packet = new()
		{
			PacketType = destination.AddressFamily == AddressFamily.InterNetwork ? IcmpPacketType.IcmpV4EchoReply : IcmpPacketType.IcmpV6EchoRequest
		};
		packet.CreateRandomPayload(size);

		return await SendEchoRequestAsync(destination, packet, timeout);
	}

	/// <summary>
	/// Sends an ICMP Echo Request (ping) with a custom packet and verifies the response.
	/// </summary>
	public static async Task<int> SendEchoRequestAsync(IPAddress destination, IcmpPacket packet, int timeout = 1000)
	{
		using Socket socket = new Socket(
			destination.AddressFamily,
			SocketType.Raw,
			destination.AddressFamily == AddressFamily.InterNetwork ? ProtocolType.Icmp : ProtocolType.IcmpV6);

		socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, timeout);
		await socket.ConnectAsync(destination, 0);

		byte[] requestBytes = packet.ToBytes();
		await socket.SendAsync(requestBytes);

		DateTime startTime = DateTime.UtcNow;
		byte[] responseBuffer = new byte[requestBytes.Length]; // Expect same length

		try
		{
			await socket.ReceiveAsync(responseBuffer);
			IcmpPacket responsePacket = IcmpPacket.ReadPacket(responseBuffer);

			// Verify response type
			var expectedReplyType = destination.AddressFamily == AddressFamily.InterNetwork ? IcmpPacketType.IcmpV4EchoReply : IcmpPacketType.IcmpV6EchoReply;
			if (responsePacket.PacketType != expectedReplyType)
				throw new IcmpException("Unexpected ICMP response type");

			// Verify that the payload matches
			if (!packet.Payload.AsSpan().SequenceEqual(responsePacket.Payload))
				throw new IcmpException("Payload mismatch");

			return (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
		}
		catch (SocketException)
		{
			return -1; // Timeout
		}
	}

	/// <summary>
	/// Performs a traceroute to a specified destination.
	/// </summary>
	public static async Task<IEnumerable<TracerouteHop>> TracerouteAsync(IPAddress destination, int maxHops = 30, int timeout = 1000)
	{
		List<TracerouteHop> hops = new();

		bool isIPv4 = destination.AddressFamily == AddressFamily.InterNetwork;
		using Socket socket = new Socket(destination.AddressFamily, SocketType.Raw, isIPv4 ? ProtocolType.Icmp : ProtocolType.IcmpV6);
		socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, timeout);

		for (int ttl = 1; ttl <= maxHops; ttl++)
		{
			TracerouteHop hop = await GetTracerouteHopAsync(destination, isIPv4, socket, ttl);
			hops.Add(hop);
			if (hop.IsFinalDestination) break;
		}

		return hops;
	}

	/// <summary>
	/// Gets a hop in a traceroute to a specified destination.
	/// </summary>
	private static async Task<TracerouteHop> GetTracerouteHopAsync(IPAddress destination, bool isIPv4, Socket socket, int ttl)
	{
		socket.SetSocketOption(isIPv4 ? SocketOptionLevel.IP : SocketOptionLevel.IPv6,
			SocketOptionName.IpTimeToLive, ttl);

		await socket.ConnectAsync(destination, 0);

		IcmpPacket packet = new()
		{
			PacketType = isIPv4 ? IcmpPacketType.IcmpV4EchoRequest : IcmpPacketType.IcmpV6EchoRequest,
		};
		packet.CreateRandomPayload(4);

		byte[] icmpPacket = packet.ToBytes();
		await socket.SendAsync(icmpPacket);

		DateTime startTime = DateTime.UtcNow;
		byte[] responseBuffer = new byte[64];

		try
		{
			await socket.ReceiveAsync(responseBuffer);
			TimeSpan rtt = DateTime.UtcNow - startTime;
			IcmpPacket response = IcmpPacket.ReadPacket(responseBuffer);

			IPAddress? hopAddress = GetIpFromResponse(response.Payload, isIPv4);

			bool isFinalDestination = response.PacketType == (isIPv4 ? IcmpPacketType.IcmpV4EchoReply : IcmpPacketType.IcmpV6EchoReply);
			bool isTimeExceeded = response.PacketType == (isIPv4 ? IcmpPacketType.IcmpV4TimeExceeded : IcmpPacketType.IcmpV6TimeExceeded);

			return new TracerouteHop(ttl, hopAddress, (int)rtt.TotalMilliseconds, isFinalDestination, isTimeExceeded);
		}
		catch (SocketException)
		{
			return new TracerouteHop(ttl, null, -1, false, false);
		}
	}

	/// <summary>
	/// Extracts the IP address from an ICMP response packet.
	/// </summary>
	private static IPAddress? GetIpFromResponse(byte[] response, bool isIPv4)
	{
		if (response.Length < 20) return null;
		return isIPv4 ? new IPAddress(response[4..8]) : new IPAddress(response[0..20]);
	}
}
