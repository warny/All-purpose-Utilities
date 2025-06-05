using System.Net;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Net.DNS;
using Utils.Net.DNS.RFC1035;

namespace UtilsTest.Net
{
    [TestClass]
    public class DNSCanonicalWriterTests
    {
        [TestMethod]
        public void CanonicalWriterProducesLowercaseNamesWithoutCompression()
        {
            // Build a DNS header with repeated names using various casing
            DNSHeader header = new DNSHeader();
            header.Requests.Add(new DNSRequestRecord("A", "EXAMPLE.COM"));
            header.QrBit = DNSQRBit.Response;
            header.Responses.Add(new DNSResponseRecord("EXAMPLE.COM", 300, new Address { IPAddress = IPAddress.Parse("1.2.3.4") }));
            header.Responses.Add(new DNSResponseRecord("ExAmPlE.CoM", 300, new Address { IPAddress = IPAddress.Parse("5.6.7.8") }));

            // Write using the canonical writer
            byte[] canonicalBytes = DNSCanonicalWriter.Default.Write(header);

            // The canonical format should never contain compression pointers (0xC0)
            CollectionAssert.DoesNotContain(canonicalBytes, (byte)0xC0);

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
    }
}
