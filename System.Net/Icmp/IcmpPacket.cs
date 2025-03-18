using System;
using System.Security.Cryptography;

namespace Utils.Net.Icmp;

/// <summary>
/// Represents an ICMP packet, including creation, serialization, and parsing.
/// </summary>
public class IcmpPacket
{
	private static readonly Random RandomGenerator = new();

	/// <summary>
	/// Gets or sets the ICMP request type (e.g., Echo Request = 8, Echo Reply = 0).
	/// </summary>
	public IcmpPacketType PacketType { get; set; }

	/// <summary>
	/// Gets or sets the ICMP request extension (Code field, usually 0 for Echo Request/Reply).
	/// </summary>
	public byte RequestExtension { get; set; } = 0;

	/// <summary>
	/// Gets or sets the identifier used to match request and response packets.
	/// </summary>
	public ushort Identifier { get; set; } = (ushort)RandomGenerator.Next(1, 65535);

	/// <summary>
	/// Gets or sets the sequence number to differentiate packets.
	/// </summary>
	public ushort SequenceNumber { get; set; } = 1;

	/// <summary>
	/// Gets or sets the payload of the ICMP packet.
	/// </summary>
	public byte[] Payload { get; set; } = [];

	/// <summary>
	/// Generates a random payload of the specified size.
	/// </summary>
	public void CreateRandomPayload(int size)
	{
		Payload = new byte[size];
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(Payload);
	}

	/// <summary>
	/// Converts the ICMP packet into a byte array suitable for network transmission.
	/// </summary>
	public byte[] ToBytes()
	{
		byte[] packet = new byte[8 + Payload.Length];
		packet[0] = (byte)PacketType; // Type
		packet[1] = RequestExtension; // Code
		packet[4] = (byte)(Identifier & 0xFF);
		packet[5] = (byte)(Identifier >> 8);
		packet[6] = (byte)(SequenceNumber & 0xFF);
		packet[7] = (byte)(SequenceNumber >> 8);

		Payload.CopyTo(packet, 8);
		SetCheckSum(packet);
		return packet;
	}

	/// <summary>
	/// Reads an ICMP packet from raw byte data, verifying the checksum.
	/// </summary>
	public static IcmpPacket ReadPacket(byte[] data)
	{
		if (data.Length < 8)
			throw new ArgumentException("Invalid ICMP packet: too short.");

		if (!ValidateChecksum(data))
			throw new ArgumentException("Invalid ICMP packet: checksum mismatch.");

		return new IcmpPacket
		{
			PacketType = (IcmpPacketType)data[0],
			RequestExtension = data[1],
			Identifier = (ushort)(data[4] | (data[5] << 8)),
			SequenceNumber = (ushort)(data[6] | (data[7] << 8)),
			Payload = data.Length > 8 ? data[8..] : []
		};
	}

	/// <summary>
	/// Computes and sets the checksum for an ICMP packet.
	/// </summary>
	private static void SetCheckSum(byte[] packet)
	{
		ushort checksum = ComputeChecksum(packet);
		packet[2] = (byte)(checksum >> 8);
		packet[3] = (byte)(checksum & 0xFF);
	}

	/// <summary>
	/// Validates the checksum of an ICMP packet.
	/// </summary>
	private static bool ValidateChecksum(byte[] data) => ComputeChecksum(data) == 0;

	/// <summary>
	/// Computes the checksum for an ICMP packet.
	/// </summary>
	private static ushort ComputeChecksum(byte[] data)
	{
		int sum = 0;
		for (int i = 0; i < data.Length; i += 2)
			sum += (data[i] << 8) + (i + 1 < data.Length ? data[i + 1] : 0);

		while ((sum >> 16) > 0)
			sum = (sum & 0xFFFF) + (sum >> 16);

		return (ushort)~sum;
	}
}
