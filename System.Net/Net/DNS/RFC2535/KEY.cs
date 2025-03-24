namespace Utils.Net.DNS.RFC2535;

/// <summary>
/// Represents a DNS KEY record (type = 25) as specified in RFC 2535 Section 3.
/// The KEY record stores a public key and associated metadata (flags, protocol, algorithm).
/// </summary>
/// <remarks>
/// <para>
/// A KEY RR can indicate a zone key (for DNSSEC), an end-entity key, or a user/account key.
/// The <c>Flags</c> field includes bits that govern usage and key ownership, and can also
/// signify the presence of a <c>no-key</c> state or an extension field.
/// </para>
/// <para>
/// If both <see cref="ProhibitedForAuthentication"/> and <see cref="ProhibitedForConfidentiality"/> are set,
/// then this record effectively asserts that there is "no key." This can be used to claim a zone
/// is unsigned (insecure), or to prohibit usage for certain cryptographic functions.
/// </para>
/// <para>
/// The <c>Protocol</c> and <c>Algorithm</c> fields follow RFC 2535 definitions, and the public key
/// (<see cref="PublicKey"/>) is algorithm-specific data. For further details on usage, see:
/// <list type="bullet">
/// <item><description>RFC 2535 for DNS security extensions</description></item>
/// <item><description>RFC 2536 (DSA keys)</description></item>
/// <item><description>RFC 2537 (RSA/MD5)</description></item>
/// <item><description>RFC 2539 (Diffie-Hellman)</description></item>
/// </list>
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x19)]
public class KEY : DNSResponseDetail
{
	/*
        3.1 KEY RDATA Format

        The KEY RR RDATA consists of:
            - a 16-bit Flags field
            - an 8-bit Protocol field
            - an 8-bit Algorithm field
            - a variable-length Public Key field (algorithm-specific)

        Bits in the Flags field control whether the key is present (or "no key"),
        whether it is used for authentication or confidentiality, and whether it is
        a zone, user, or host key, among other options.

        The Protocol octet defines how the key is intended to be used (e.g., DNSSEC = 3,
        IPSEC = 4, TLS = 1, etc.). The Algorithm octet indicates the cryptographic algorithm
        used (RSA, DSA, etc.). The Public Key portion is algorithm-dependent.
    */

	/// <summary>
	/// Gets the 16-bit flags field which indicates key usage, key owner type, extension flags,
	/// and signatory bits. This is manipulated through properties such as
	/// <see cref="ProhibitedForAuthentication"/>, <see cref="ProhibitedForConfidentiality"/>,
	/// <see cref="Extended"/>, <see cref="KeyOwner"/>, and <see cref="SignatoryField"/>.
	/// </summary>
	[DNSField]
	public ushort Flags { get; private set; }

	/// <summary>
	/// Gets or sets the 8-bit protocol value. Typically <c>3</c> (DNSSEC),
	/// but can be TLS (1), email (2), IPSEC (4), or others as assigned by IANA.
	/// </summary>
	[DNSField]
	public Protocol Protocol { get; set; }

	/// <summary>
	/// Gets or sets the cryptographic algorithm. Common values include
	/// <c>3</c> (DSA), <c>1</c> (RSA/MD5), etc. for DNSSEC usage.
	/// </summary>
	[DNSField]
	public Algorithm Algorithm { get; set; }

	/// <summary>
	/// An optional 16-bit extension field present only if <see cref="Extended"/> is set in the <see cref="Flags"/>.
	/// </summary>
	[DNSField(Condition = "Extended")]
	public ushort Extension { get; set; }

	/// <summary>
	/// Gets or sets the raw public key bytes. This is algorithm-specific data
	/// whose format depends on the <see cref="Algorithm"/>.
	/// </summary>
	/// <remarks>
	/// If the <c>No-Key</c> bits are set in <see cref="Flags"/>,
	/// the <see cref="PublicKey"/> should be omitted or ignored.
	/// </remarks>
	[DNSField]
	public byte[] PublicKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the key is prohibited for authentication use
	/// (flag bits 0-1 set to <c>10</c> in the specification).
	/// </summary>
	public bool ProhibitedForAuthentication
	{
		get => (Flags & KeyFlags.ProhibitedForAuthentication) != 0;
		set => Flags = (ushort)((Flags & ~KeyFlags.ProhibitedForAuthentication)
								| (value ? KeyFlags.ProhibitedForAuthentication : 0));
	}

	/// <summary>
	/// Gets or sets a value indicating whether the key is prohibited for confidentiality use
	/// (flag bits 0-1 set to <c>01</c> in the specification).
	/// </summary>
	public bool ProhibitedForConfidentiality
	{
		get => (Flags & KeyFlags.ProhibitedForConfidentiality) != 0;
		set => Flags = (ushort)((Flags & ~KeyFlags.ProhibitedForConfidentiality)
								| (value ? KeyFlags.ProhibitedForConfidentiality : 0));
	}

	/// <summary>
	/// Gets or sets a value indicating whether the "extension" bit (bit 3) is set,
	/// indicating additional 16 bits of flags are present in <see cref="Extension"/>.
	/// </summary>
	public bool Extended
	{
		get => (Flags & KeyFlags.Extension) != 0;
		set => Flags = (ushort)((Flags & ~KeyFlags.Extension) | (value ? KeyFlags.Extension : 0));
	}

	/// <summary>
	/// Gets or sets the key owner (user/account, zone key, host/entity, or reserved).
	/// This corresponds to bits 6-7 in the flags.
	/// </summary>
	public KeyOwner KeyOwner
	{
		get => (KeyOwner)(Flags & KeyFlagsMasks.KeyOwner);
		set => Flags = (ushort)((Flags & ~KeyFlagsMasks.KeyOwner) | (ushort)value);
	}

	/// <summary>
	/// Gets or sets the 4-bit signatory field (bits 12-15),
	/// indicating whether the key can validly sign updates (RFC 2137).
	/// </summary>
	public byte SignatoryField
	{
		get => (byte)(Flags & KeyFlagsMasks.SignatoryField);
		set => Flags = (ushort)((Flags & ~KeyFlagsMasks.SignatoryField)
								| ((ushort)value & KeyFlagsMasks.SignatoryField));
	}
}

/// <summary>
/// Contains constants for bit masks within the <see cref="KEY.Flags"/> field.
/// </summary>
static class KeyFlags
{
	// Bits 0 and 1 : Key usage
	public const ushort ProhibitedForAuthentication = 0b_1000_0000_0000_0000;  // bit 0 set
	public const ushort ProhibitedForConfidentiality = 0b_0100_0000_0000_0000; // bit 1 set

	// Bit 3 : Extension flag
	public const ushort Extension = 0b_0001_0000_0000_0000;

	// Bits 6 and 7 : Key owner
	public const ushort UserOrAccountKey = 0b_0000_0000_0000_0000; // 00
	public const ushort ZoneKey = 0b_0000_0010_0000_0000; // 01
	public const ushort NonZoneKey = 0b_0000_0100_0000_0000; // 10
	public const ushort ReservedUseKey = 0b_0000_0110_0000_0000; // 11
}

/// <summary>
/// Enumerates possible owner types stored in bits 6-7 of <see cref="KEY.Flags"/>.
/// </summary>
public enum KeyOwner : ushort
{
	/// <summary>
	/// A user or account key, typically used at an end-entity level.
	/// </summary>
	UserOrAccountKey = KeyFlags.UserOrAccountKey,

	/// <summary>
	/// A zone key, typically used for DNSSEC at zone apexes.
	/// </summary>
	ZoneKey = KeyFlags.ZoneKey,

	/// <summary>
	/// A non-zone host or entity key.
	/// </summary>
	NonZoneKey = KeyFlags.NonZoneKey,

	/// <summary>
	/// Reserved usage bits.
	/// </summary>
	ReservedUseKey = KeyFlags.ReservedUseKey
}

/// <summary>
/// Contains masks to extract portions of <see cref="KEY.Flags"/>.
/// </summary>
static class KeyFlagsMasks
{
	/// <summary>
	/// Bits 0 and 1 : Key usage
	/// </summary>
	public const ushort KeyUsage = 0b_1100_0000_0000_0000;

	/// <summary>
	/// Bit 3 : Extension flag
	/// </summary>
	public const ushort Extension = 0b_0001_0000_0000_0000;

	/// <summary>
	/// Bits 6 and 7 : Key owner
	/// </summary>
	public const ushort KeyOwner = 0b_0000_0110_0000_0000;

	/// <summary>
	/// Bits 12-15 : SignatoryField
	/// <para>
	/// If non-zero, indicates the key can sign dynamic updates (RFC 2137).
	/// Zone keys (<c>KeyOwner.ZoneKey</c>) always have authority to sign
	/// RRs in the zone regardless of the signatory bits.
	/// </para>
	/// </summary>
	public const ushort SignatoryField = 0b_0000_0000_0000_1111;
}
