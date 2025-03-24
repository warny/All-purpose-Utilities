using System;
using Utils.Net.DNS;

namespace Utils.Net.DNS.RFC4398
{
	/// <summary>
	/// Represents a CERT record, as specified in RFC 4398, "Storing Certificates in the DNS."
	/// CERT records are used to store certificates or Certificate Revocation Lists (CRLs) in DNS.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The CERT resource record (RR) has the structure given below. Its RR type code is 37.
	/// </para>
	/// <para>
	/// The RDATA for a CERT RR is formatted as follows:
	/// </para>
	/// <code>
	///                               1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
	///           0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
	///          +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	///          |             type              |             key tag           |
	///          +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	///          |   algorithm   |                                               /
	///          +---------------+            certificate or CRL                 /
	///          /                                                               /
	///          +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	/// </code>
	/// <para>
	/// - <b>type</b>: The certificate type as defined in Section 2.1 of RFC 4398.
	/// - <b>key tag</b>: A 16-bit field computed for the key embedded in the certificate,
	///   using the RRSIG Key Tag algorithm described in Appendix B of [12]. It is used to
	///   efficiently select applicable CERT records for a particular key.
	/// - <b>algorithm</b>: The algorithm field has the same meaning as in DNSKEY and RRSIG RRs
	///   (e.g., RSA/MD5, DSA). A value of zero indicates that the algorithm is unknown to a secure DNS.
	/// - <b>certificate or CRL</b>: The remaining data is the certificate or CRL, stored in a
	///   format appropriate for the key in question. Prior to computing the key tag, the key MUST be
	///   transformed into the format it would have as the public key portion of a DNSKEY RR.
	///   If the key is not applicable to a recognized algorithm or does not meet size restrictions,
	///   the algorithm field MUST be zero and the key tag is meaningless.
	/// </para>
	/// </remarks>
	[DNSRecord(DNSClass.IN, 0x25)]
	public class CERT : DNSResponseDetail
	{
		/// <summary>
		/// Gets or sets the certificate type, as defined in RFC 4398 Section 2.1.
		/// </summary>
		[DNSField]
		public CertificateTypes Type { get; set; }

		/// <summary>
		/// Gets or sets the Key Tag field, a 16-bit identifier computed from the certificate's key.
		/// This field is used to efficiently select which CERT record may apply to a given key.
		/// </summary>
		[DNSField]
		public ushort KeyTag { get; set; }

		/// <summary>
		/// Gets or sets the Algorithm field, an 8-bit value that indicates the cryptographic
		/// algorithm used for the certificate. A value of zero indicates that the algorithm is unknown.
		/// </summary>
		[DNSField]
		public Algorithm Algorithm { get; set; }

		/// <summary>
		/// Gets or sets the certificate or CRL data. This field contains the actual certificate
		/// (or Certificate Revocation List) in a format appropriate to the specified algorithm.
		/// </summary>
		[DNSField]
		public byte[] ObjectDatas { get; set; }
	}
}
