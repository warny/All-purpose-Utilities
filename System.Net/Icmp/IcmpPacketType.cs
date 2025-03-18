namespace Utils.Net.Icmp;

public enum IcmpPacketType : byte
{
	IcmpV4EchoReply = 0,
	IcmpV4EchoRequest = 8,
	IcmpV6EchoRequest = 128,
	IcmpV6EchoReply = 129,
	IcmpV4TimeExceeded = 11,
	IcmpV6TimeExceeded = 3
}
