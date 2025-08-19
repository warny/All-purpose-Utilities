namespace Utils.Net.DNS.RFC4255;

/// <summary>
/// Represents an SSHFP (SSH Fingerprint) record as defined in RFC 4255.
/// An SSHFP record stores a fingerprint of an SSH public key, allowing clients to
/// verify the authenticity of an SSH server by comparing the fingerprint from DNS with the key provided during the SSH connection.
/// </summary>
/// <remarks>
/// <para>
/// The SSHFP record contains three fields:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Algorithm</b>: The algorithm used for the SSH key. This field is of type <see cref="SSHAlgorithm"/>,
///       and its value indicates the public key algorithm (e.g. RSA, DSS).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Fingerprint Type</b>: The hash algorithm used to generate the fingerprint.
///       This field is of type <see cref="SSHFingerprintType"/> and typically specifies SHA-1.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Fingerprint</b>: The actual fingerprint data, stored as a byte array.
///       This is the hash of the SSH public key, and its length depends on the hash algorithm used.
///     </description>
///   </item>
/// </list>
/// <para>
/// When an SSHFP record is published in DNS, SSH clients can use it to verify that the host key
/// presented by the server during connection setup matches the fingerprint obtained from DNS.
/// This provides an additional layer of security by ensuring the key is trusted.
/// </para>
/// </remarks>
[DNSRecord(DNSClassId.IN, 0x2C)]
[DNSTextRecord("{Algorithm} {FingerPrintType} {FingerPrint}")]
public class SSHFP : DNSResponseDetail
{
	/// <summary>
	/// Gets or sets the SSH algorithm identifier.
	/// This indicates the type of SSH key used (e.g., RSA or DSS).
	/// </summary>
	[DNSField]
	public SSHAlgorithm Algorithm { get; set; }

	/// <summary>
	/// Gets or sets the fingerprint type.
	/// This indicates the hash algorithm used to generate the fingerprint (e.g., SHA-1).
	/// </summary>
	[DNSField]
	public SSHFingerprintType FingerPrintType { get; set; }

	/// <summary>
	/// Gets or sets the fingerprint data as a byte array.
	/// This is the result of hashing the SSH public key using the specified fingerprint type.
	/// </summary>
	[DNSField]
	public byte[] FingerPrint { get; set; }
}
