namespace Utils.Net.DNS.RFC2535;

/// <summary>
/// Represents an NXT (Next) record in DNS as specified by RFC 2535.
/// </summary>
/// <remarks>
/// <para>
/// The NXT record is used in DNSSEC to prove the non-existence of certain names or types
/// in a zone. It provides a "next domain name" which, together with a type bitmap, defines
/// an interval in the canonical ordering of names in the zone. Any name not covered by an NXT
/// record is guaranteed not to exist in the zone.
/// </para>
/// <para>
/// The NXT record RDATA consists of:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>NextDomainName</b>: A domain name which is the next name in the zone's canonical order.
///       If the zone is considered circular, the last NXT record will point back to the zone apex.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>TypeBitMap</b>: A variable-length bitmap where each bit corresponds to a DNS RR type.
///       A bit set to 1 indicates that at least one RR of that type exists for the owner name.
///       The bit for type 0 is always 0 because type 0 is not used. The bitmap must be interpreted
///       according to the rules in RFC 2535.
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// In a secure DNS response, an NXT record (and its associated signature) is included in the
/// authority section to indicate that no records exist in the gap between the owner name of the
/// NXT and the <b>NextDomainName</b> in canonical order. This mechanism is part of the DNSSEC
/// protocol for securely denying the existence of domain names or RR types.
/// </para>
/// </remarks>
[DNSRecord(DNSClass.IN, 0x1E)]
[DNSTextRecord("{NextDomainName} {TypeBitMap}")]
public class NXT : DNSResponseDetail
{
	/*
5. Non-existent Names and Types

   The SIG RR mechanism described in Section 4 above provides strong
   authentication of RRs that exist in a zone.  But it is not clear
   above how to verifiably deny the existence of a name in a zone or a
   type for an existent name.

   The nonexistence of a name in a zone is indicated by the NXT ("next")
   RR for a name interval containing the nonexistent name. An NXT RR or
   RRs and its or their SIG(s) are returned in the authority section,
   along with the error, if the server is security aware.  The same is
   true for a non-existent type under an existing name except that there
   is no error indication other than an empty answer section
   accompanying the NXT(s). This is a change in the existing standard
   [RFCs 1034/1035] which contemplates only NS and SOA RRs in the
   authority section. NXT RRs will also be returned if an explicit query
   is made for the NXT type.

   The existence of a complete set of NXT records in a zone means that
   any query for any name and any type to a security aware server
   serving the zone will result in an reply containing at least one
   signed RR unless it is a query for delegation point NS or glue A or
   AAAA RRs.

5.1 The NXT Resource Record

   The NXT resource record is used to securely indicate that RRs with an
   owner name in a certain name interval do not exist in a zone and to
   indicate what RR types are present for an existing name.
   The owner name of the NXT RR is an existing name in the zone.  It's
   RDATA is a "next" name and a type bit map. Thus the NXT RRs in a zone
   create a chain of all of the literal owner names in that zone,
   including unexpanded wildcards but omitting the owner name of glue
   address records unless they would otherwise be included. This implies
   a canonical ordering of all domain names in a zone as described in
   Section 8. The presence of the NXT RR means that no name between its
   owner name and the name in its RDATA area exists and that no other
   types exist under its owner name.

   There is a potential problem with the last NXT in a zone as it wants
   to have an owner name which is the last existing name in canonical
   order, which is easy, but it is not obvious what name to put in its
   RDATA to indicate the entire remainder of the name space.  This is
   handled by treating the name space as circular and putting the zone
   name in the RDATA of the last NXT in a zone.

   The NXT RRs for a zone SHOULD be automatically calculated and added
   to the zone when SIGs are added.  The NXT RR's TTL SHOULD NOT exceed
   the zone minimum TTL.

   The type number for the NXT RR is 30.

   NXT RRs are only signed by zone level keys.

5.2 NXT RDATA Format

   The RDATA for an NXT RR consists simply of a domain name followed by
   a bit map, as shown below.

						1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
	0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |                  next domain name                             /
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
   |                    type bit map                               /
   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

   The NXT RR type bit map format currently defined is one bit per RR
   type present for the owner name.  A one bit indicates that at least
   one RR of that type is present for the owner name.  A zero indicates
   that no such RR is present.  All bits not specified because they are
   beyond the end of the bit map are assumed to be zero.  Note that bit
   30, for NXT, will always be on so the minimum bit map length is
   actually four octets. Trailing zero octets are prohibited in this
   format.  The first bit represents RR type zero (an illegal type which
   can not be present) and so will be zero in this format.  This format
   is not used if there exists an RR with a type number greater than
   127.  If the zero bit of the type bit map is a one, it indicates that
   a different format is being used which will always be the case if a
   type number greater than 127 is present.

   The domain name may be compressed with standard DNS name compression
   when being transmitted over the network.  The size of the bit map can
   be inferred from the RDLENGTH and the length of the next domain name.

5.3 Additional Complexity Due to Wildcards

   Proving that a non-existent name response is correct or that a
   wildcard expansion response is correct makes things a little more
   complex.

   In particular, when a non-existent name response is returned, an NXT
   must be returned showing that the exact name queried did not exist
   and, in general, one or more additional NXT's need to be returned to
   also prove that there wasn't a wildcard whose expansion should have
   been returned. (There is no need to return multiple copies of the
   same NXT.) These NXTs, if any, are returned in the authority section
   of the response.

   Furthermore, if a wildcard expansion is returned in a response, in
   general one or more NXTs needs to also be returned in the authority
   section to prove that no more specific name (including possibly more
   specific wildcards in the zone) existed on which the response should
   have been based.

5.4 Example

   Assume zone foo.nil has entries for

		  big.foo.nil,
		  medium.foo.nil.
		  small.foo.nil.
		  tiny.foo.nil.

   Then a query to a security aware server for huge.foo.nil would
   produce an error reply with an RCODE of NXDOMAIN and the authority
   section data including something like the following:

   foo.nil.    NXT big.foo.nil NS KEY SOA NXT ;prove no *.foo.nil
   foo.nil.    SIG NXT 1 2 ( ;type-cov=NXT, alg=1, labels=2
					19970102030405 ;signature expiration
					19961211100908 ;signature inception
					2143           ;key identifier
					foo.nil.       ;signer
   AIYADP8d3zYNyQwW2EM4wXVFdslEJcUx/fxkfBeH1El4ixPFhpfHFElxbvKoWmvjDTCm
   fiYy2X+8XpFjwICHc398kzWsTMKlxovpz2FnCTM= ;signature (640 bits)
						  )
   big.foo.nil. NXT medium.foo.nil. A MX SIG NXT ;prove no huge.foo.nil
   big.foo.nil. SIG NXT 1 3 ( ;type-cov=NXT, alg=1, labels=3
					19970102030405 ;signature expiration
					19961211100908 ;signature inception
					2143           ;key identifier
					foo.nil.       ;signer
	MxFcby9k/yvedMfQgKzhH5er0Mu/vILz45IkskceFGgiWCn/GxHhai6VAuHAoNUz4YoU
	1tVfSCSqQYn6//11U6Nld80jEeC8aTrO+KKmCaY= ;signature (640 bits)
							 )
   Note that this response implies that big.foo.nil is an existing name
   in the zone and thus has other RR types associated with it than NXT.
   However, only the NXT (and its SIG) RR appear in the response to this
   query for huge.foo.nil, which is a non-existent name.

5.5 Special Considerations at Delegation Points

   A name (other than root) which is the head of a zone also appears as
   the leaf in a superzone.  If both are secure, there will always be
   two different NXT RRs with the same name.  They can be easily
   distinguished by their signers, the next domain name fields, the
   presence of the SOA type bit, etc.  Security aware servers should
   return the correct NXT automatically when required to authenticate
   the non-existence of a name and both NXTs, if available, on explicit
   query for type NXT.

   Non-security aware servers will never automatically return an NXT and
   some old implementations may only return the NXT from the subzone on
   explicit queries.

5.6 Zone Transfers

   The subsections below describe how full and incremental zone
   transfers are secured.

   SIG RRs secure all authoritative RRs transferred for both full and
   incremental [RFC 1995] zone transfers.  NXT RRs are an essential
   element in secure zone transfers and assure that every authoritative
   name and type will be present; however, if there are multiple SIGs
   with the same name and type covered, a subset of the SIGs could be
   sent as long as at least one is present and, in the case of unsigned
   delegation point NS or glue A or AAAA RRs a subset of these RRs or
   simply a modified set could be sent as long as at least one of each
   type is included.

   When an incremental or full zone transfer request is received with
   the same or newer version number than that of the server's copy of
   the zone, it is replied to with just the SOA RR of the server's
   current version and the SIG RRset verifying that SOA RR.

   The complete NXT chains specified in this document enable a resolver
   to obtain, by successive queries chaining through NXTs, all of the
   names in a zone even if zone transfers are prohibited.  Different
   format NXTs may be specified in the future to avoid this.

5.6.1 Full Zone Transfers

   To provide server authentication that a complete transfer has
   occurred, transaction authentication SHOULD be used on full zone
   transfers.  This provides strong server based protection for the
   entire zone in transit.

5.6.2 Incremental Zone Transfers

   Individual RRs in an incremental (IXFR) transfer [RFC 1995] can be
   verified in the same way as for a full zone transfer and the
   integrity of the NXT name chain and correctness of the NXT type bits
   for the zone after the incremental RR deletes and adds can check each
   disjoint area of the zone updated.  But the completeness of an
   incremental transfer can not be confirmed because usually neither the
   deleted RR section nor the added RR section has a compete zone NXT
   chain.  As a result, a server which securely supports IXFR must
   handle IXFR SIG RRs for each incremental transfer set that it
   maintains.

   The IXFR SIG is calculated over the incremental zone update
   collection of RRs in the order in which it is transmitted: old SOA,
   then deleted RRs, then new SOA and added RRs.  Within each section,
   RRs must be ordered as specified in Section 8.  If condensation of
   adjacent incremental update sets is done by the zone owner, the
   original IXFR SIG for each set included in the condensation must be
   discarded and a new on IXFR SIG calculated to cover the resulting
   condensed set.

   The IXFR SIG really belongs to the zone as a whole, not to the zone
   name.  Although it SHOULD be correct for the zone name, the labels
   field of an IXFR SIG is otherwise meaningless.  The IXFR SIG is only
   sent as part of an incremental zone transfer.  After validation of
   the IXFR SIG, the transferred RRs MAY be considered valid without
   verification of the internal SIGs if such trust in the server
   conforms to local policy.
*/
	/// <summary>
	/// Gets or sets the next domain name in the zone's canonical order.
	/// This field is used to define the interval in which no RR exists.
	/// </summary>
	[DNSField]
	public DNSDomainName NextDomainName { get; set; }

	/// <summary>
	/// Gets or sets the type bitmap which indicates the RR types present for the owner name.
	/// Each bit corresponds to an RR type; a set bit means that at least one RR of that type exists.
	/// Trailing zero octets are prohibited, and the bitmap must be interpreted in accordance
	/// with RFC 2535.
	/// </summary>
	[DNSField]
	public byte[] TypeBitMap { get; set; }
}