using System.Net;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;

namespace UtilsTest.Net;

[TestClass]
public class DNSCanonicalWriterTests
{
    [TestMethod]
    public void CanonicalWriterProducesLowercaseNamesWithoutCompression()
    {
        // Build a DNS header with repeated names using various casing.
        // ID is fixed to avoid a random 0xC0 byte that would falsely trip the compression check.
        DNSHeader header = new DNSHeader { ID = 0x1234 };
        header.Requests.Add(new DNSRequestRecord("A", "EXAMPLE.COM"));
        header.QrBit = DNSQRBit.Response;
        header.Responses.Add(new DNSResponseRecord("EXAMPLE.COM", 300, new Address { IPAddress = IPAddress.Parse("1.2.3.4") }));
        header.Responses.Add(new DNSResponseRecord("ExAmPlE.CoM", 300, new Address { IPAddress = IPAddress.Parse("5.6.7.8") }));

        // Write using the canonical writer
        byte[] canonicalBytes = DNSCanonicalWriter.Default.Write(header);

        // The canonical format must not use DNS name compression in any domain-name field.
        // We walk the packet structure and check that every label-length byte satisfies
        // (b & 0xC0) != 0xC0; only those bytes matter — scanning the whole packet would
        // produce false positives for 0xC0 in IDs, TTLs, or RDATA (e.g. 192.x.x.x).
        int qdCount = (canonicalBytes[4] << 8) | canonicalBytes[5];
        int anCount = (canonicalBytes[6] << 8) | canonicalBytes[7];
        AssertNoDnsNameCompression(canonicalBytes, qdCount, anCount);

        // Reading back should yield lower case domain names
        DNSHeader decoded = DNSPacketReader.Default.Read(canonicalBytes);
        Assert.AreEqual("example.com", decoded.Requests[0].Name.Value);
        Assert.AreEqual("example.com", decoded.Responses[0].Name.Value);
        Assert.AreEqual("example.com", decoded.Responses[1].Name.Value);
    }

    [TestMethod]
    public void CanonicalRecordSignatureVerification()
    {
        // Create a simple A record and compute its canonical form
        DNSResponseRecord addressRecord = new DNSResponseRecord("example.com", 300,
            new Address { IPAddress = IPAddress.Parse("1.2.3.4") });

        byte[] canonical = DNSCanonicalWriter.Default.Write(addressRecord);

        using RSA rsa = RSA.Create();
        byte[] signatureBytes = rsa.SignData(canonical, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Build a RRSIG record containing the signature
        var signature = new Utils.Net.DNS.RFC4034.RRSIG
        {
            TypeCovered = 0x01, // A record
            Algorithm = (byte)Algorithm.RSA_SHA1,
            Labels = 2, // example.com
            OriginalTTL = 300,
            SignatureExpiration = 0,
            SignatureInception = 0,
            KeyTag = 0,
            SignerName = "example.com",
            Signature = signatureBytes
        };

        DNSResponseRecord sigRecord = new DNSResponseRecord("example.com", 300, signature);

        // Serialize a DNS header containing both the address record and its signature
        DNSHeader header = new DNSHeader { QrBit = DNSQRBit.Response };
        header.Responses.Add(addressRecord);
        header.Responses.Add(sigRecord);

        byte[] datagram = DNSPacketWriter.Default.Write(header);

        // Read the header back and verify equality
        DNSHeader decoded = DNSPacketReader.Default.Read(datagram);
        Assert.AreEqual(header, decoded, DNSHeadersComparer.Default);

        // Validate the signature using the canonical form of the read record
        byte[] canonicalRead = DNSCanonicalWriter.Default.Write(decoded.Responses[0]);
        var decodedSig = (Utils.Net.DNS.RFC4034.RRSIG)decoded.Responses[1].RData;
        bool valid = rsa.VerifyData(canonicalRead, decodedSig.Signature,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.IsTrue(valid);
    }

    /// <summary>
    /// Walks every domain-name field in a raw DNS packet and asserts that no label-length
    /// byte uses the compression-pointer prefix (top two bits both set: <c>(b &amp; 0xC0) == 0xC0</c>).
    /// </summary>
    private static void AssertNoDnsNameCompression(byte[] packet, int qdCount, int anCount)
    {
        int pos = 12; // skip the fixed 12-byte DNS header

        for (int i = 0; i < qdCount; i++)
        {
            pos = SkipDomainNameAssertingNoCompression(packet, pos);
            pos += 4; // QTYPE (2) + QCLASS (2)
        }

        for (int i = 0; i < anCount; i++)
        {
            pos = SkipDomainNameAssertingNoCompression(packet, pos);
            pos += 8; // TYPE (2) + CLASS (2) + TTL (4)
            int rdLength = (packet[pos] << 8) | packet[pos + 1];
            pos += 2 + rdLength; // RDLENGTH (2) + RDATA
        }
    }

    /// <summary>
    /// Advances past a single DNS domain name starting at <paramref name="pos"/>, asserting
    /// that no label-length byte carries a compression-pointer prefix.
    /// </summary>
    /// <returns>The position immediately after the terminating null byte.</returns>
    private static int SkipDomainNameAssertingNoCompression(byte[] packet, int pos)
    {
        while (pos < packet.Length)
        {
            byte b = packet[pos];
            if (b == 0)
                return pos + 1;
            Assert.IsFalse(
                (b & 0xC0) == 0xC0,
                $"DNS compression pointer (0xC0) detected at packet offset {pos}: byte = 0x{b:X2}");
            pos += 1 + b; // length byte + label content
        }
        return pos;
    }
}
