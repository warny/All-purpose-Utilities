using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Net.DNS.RFC2535;

[DNSRecord(DNSClass.IN, 0x18)]
public class SIG : DNSResponseDetail
{
    /*
		4. The SIG Resource Record

		   The SIG or "signature" resource record (RR) is the fundamental way
		   that data is authenticated in the secure Domain Name System (DNS). As
		   such it is the heart of the security provided.

		   The SIG RR unforgably authenticates an RRset [RFC 2181] of a
		   particular type, class, and name and binds it to a time interval and
		   the signer's domain name.  This is done using cryptographic
		   techniques and the signer's private key.  The signer is frequently
		   the owner of the zone from which the RR originated.

		   The type number for the SIG RR type is 24.

		4.1 SIG RDATA Format

		   The RDATA portion of a SIG RR is as shown below.  The integrity of
		   the RDATA information is protected by the signature field.


								   1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
			   0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
			  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
			  |        type covered           |  algorithm    |     labels    |
			  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
			  |                         original TTL                          |
			  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
			  |                      signature expiration                     |
			  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
			  |                      signature inception                      |
			  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
			  |            key  tag           |                               |
			  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+         signer's name         +
			  |                                                               /
			  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-/
			  /                                                               /
			  /                            signature                          /
			  /                                                               /
			  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

		4.1.1 Type Covered Field

		   The "type covered" is the type of the other RRs covered by this SIG.

		4.1.2 Algorithm Number Field

		   This octet is as described in section 3.2.

		4.1.3 Labels Field

		   The "labels" octet is an unsigned count of how many labels there are
		   in the original SIG RR owner name not counting the null label for
		   root and not counting any initial "*" for a wildcard.  If a secured
		   retrieval is the result of wild card substitution, it is necessary
		   for the resolver to use the original form of the name in verifying
		   the digital signature.  This field makes it easy to determine the
		   original form.

		   If, on retrieval, the RR appears to have a longer name than indicated
		   by "labels", the resolver can tell it is the result of wildcard
		   substitution.  If the RR owner name appears to be shorter than the
		   labels count, the SIG RR must be considered corrupt and ignored.  The
		   maximum number of labels allowed in the current DNS is 127 but the
		   entire octet is reserved and would be required should DNS names ever
		   be expanded to 255 labels.  The following table gives some examples.
		   The value of "labels" is at the top, the retrieved owner name on the
		   left, and the table entry is the name to use in signature
		   verification except that "bad" means the RR is corrupt.

		   labels= |  0  |   1  |    2   |      3   |      4   |
		   --------+-----+------+--------+----------+----------+
				  .|   . | bad  |  bad   |    bad   |    bad   |
				 d.|  *. |   d. |  bad   |    bad   |    bad   |
			   c.d.|  *. | *.d. |   c.d. |    bad   |    bad   |
			 b.c.d.|  *. | *.d. | *.c.d. |   b.c.d. |    bad   |
		   a.b.c.d.|  *. | *.d. | *.c.d. | *.b.c.d. | a.b.c.d. |

		4.1.4 Original TTL Field

		   The "original TTL" field is included in the RDATA portion to avoid
		   (1) authentication problems that caching servers would otherwise
		   cause by decrementing the real TTL field and (2) security problems
		   that unscrupulous servers could otherwise cause by manipulating the
		   real TTL field.  This original TTL is protected by the signature
		   while the current TTL field is not.

		   NOTE:  The "original TTL" must be restored into the covered RRs when
		   the signature is verified (see Section 8).  This generally implies
		   that all RRs for a particular type, name, and class, that is, all the
		   RRs in any particular RRset, must have the same TTL to start with.

		4.1.5 Signature Expiration and Inception Fields

		   The SIG is valid from the "signature inception" time until the
		   "signature expiration" time.  Both are unsigned numbers of seconds
		   since the start of 1 January 1970, GMT, ignoring leap seconds.  (See
		   also Section 4.4.)  Ring arithmetic is used as for DNS SOA serial
		   numbers [RFC 1982] which means that these times can never be more
		   than about 68 years in the past or the future.  This means that these
		   times are ambiguous modulo ~136.09 years.  However there is no
		   security flaw because keys are required to be changed to new random
		   keys by [RFC 2541] at least every five years.  This means that the
		   probability that the same key is in use N*136.09 years later should
		   be the same as the probability that a random guess will work.

		   A SIG RR may have an expiration time numerically less than the
		   inception time if the expiration time is near the 32 bit wrap around
		   point and/or the signature is long lived.

		   (To prevent misordering of network requests to update a zone
		   dynamically, monotonically increasing "signature inception" times may
		   be necessary.)

		   A secure zone must be considered changed for SOA serial number
		   purposes not only when its data is updated but also when new SIG RRs
		   are inserted (ie, the zone or any part of it is re-signed).

		4.1.6 Key Tag Field

		   The "key Tag" is a two octet quantity that is used to efficiently
		   select between multiple keys which may be applicable and thus check
		   that a public key about to be used for the computationally expensive
		   effort to check the signature is possibly valid.  For algorithm 1
		   (MD5/RSA) as defined in [RFC 2537], it is the next to the bottom two
		   octets of the public key modulus needed to decode the signature
		   field.  That is to say, the most significant 16 of the least
		   significant 24 bits of the modulus in network (big endian) order. For
		   all other algorithms, including private algorithms, it is calculated
		   as a simple checksum of the KEY RR as described in Appendix C.

		4.1.7 Signer's Name Field

		   The "signer's name" field is the domain name of the signer generating
		   the SIG RR.  This is the owner name of the public KEY RR that can be
		   used to verify the signature.  It is frequently the zone which
		   contained the RRset being authenticated.  Which signers should be
		   authorized to sign what is a significant resolver policy question as
		   discussed in Section 6. The signer's name may be compressed with
		   standard DNS name compression when being transmitted over the
		   network.

		4.1.8 Signature Field

		   The actual signature portion of the SIG RR binds the other RDATA
		   fields to the RRset of the "type covered" RRs with that owner name
		   and class.  This covered RRset is thereby authenticated.  To
		   accomplish this, a data sequence is constructed as follows:

				 data = RDATA | RR(s)...

		   where "|" is concatenation,

		   RDATA is the wire format of all the RDATA fields in the SIG RR itself
		   (including the canonical form of the signer's name) before but not
		   including the signature, and

		   RR(s) is the RRset of the RR(s) of the type covered with the same
		   owner name and class as the SIG RR in canonical form and order as
		   defined in Section 8.

		   How this data sequence is processed into the signature is algorithm
		   dependent.  These algorithm dependent formats and procedures are
		   described in separate documents (Section 3.2).
		   SIGs SHOULD NOT be included in a zone for any "meta-type" such as
		   ANY, AXFR, etc. (but see section 5.6.2 with regard to IXFR).

		4.1.8.1 Calculating Transaction and Request SIGs

		   A response message from a security aware server may optionally
		   contain a special SIG at the end of the additional information
		   section to authenticate the transaction.

		   This SIG has a "type covered" field of zero, which is not a valid RR
		   type.  It is calculated by using a "data" (see Section 4.1.8) of the
		   entire preceding DNS reply message, including DNS header but not the
		   IP header and before the reply RR counts have been adjusted for the
		   inclusion of any transaction SIG, concatenated with the entire DNS
		   query message that produced this response, including the query's DNS
		   header and any request SIGs but not its IP header.  That is

			  data = full response (less transaction SIG) | full query

		   Verification of the transaction SIG (which is signed by the server
		   host key, not the zone key) by the requesting resolver shows that the
		   query and response were not tampered with in transit, that the
		   response corresponds to the intended query, and that the response
		   comes from the queried server.

		   A DNS request may be optionally signed by including one or more SIGs
		   at the end of the query. Such SIGs are identified by having a "type
		   covered" field of zero. They sign the preceding DNS request message
		   including DNS header but not including the IP header or any request
		   SIGs at the end and before the request RR counts have been adjusted
		   for the inclusions of any request SIG(s).

		   WARNING: Request SIGs are unnecessary for any currently defined
		   request other than update [RFC 2136, 2137] and will cause some old
		   DNS servers to give an error return or ignore a query.  However, such
		   SIGs may in the future be needed for other requests.

		   Except where needed to authenticate an update or similar privileged
		   request, servers are not required to check request SIGs.

		4.2 SIG RRs in the Construction of Responses

		   Security aware DNS servers SHOULD, for every authenticated RRset the
		   query will return, attempt to send the available SIG RRs which
		   authenticate the requested RRset.  The following rules apply to the
		   inclusion of SIG RRs in responses:
			 1. when an RRset is placed in a response, its SIG RR has a higher
				priority for inclusion than additional RRs that may need to be
				included.  If space does not permit its inclusion, the response
				MUST be considered truncated except as provided in 2 below.

			 2. When a SIG RR is present in the zone for an additional
				information section RR, the response MUST NOT be considered
				truncated merely because space does not permit the inclusion of
				the SIG RR with the additional information.

			 3. SIGs to authenticate glue records and NS RRs for subzones at a
				delegation point are unnecessary and MUST NOT be sent.

			 4. If a SIG covers any RR that would be in the answer section of
				the response, its automatic inclusion MUST be in the answer
				section.  If it covers an RR that would appear in the authority
				section, its automatic inclusion MUST be in the authority
				section.  If it covers an RR that would appear in the additional
				information section it MUST appear in the additional information
				section.  This is a change in the existing standard [RFCs 1034,
				1035] which contemplates only NS and SOA RRs in the authority
				section.

			 5. Optionally, DNS transactions may be authenticated by a SIG RR at
				the end of the response in the additional information section
				(Section 4.1.8.1).  Such SIG RRs are signed by the DNS server
				originating the response.  Although the signer field MUST be a
				name of the originating server host, the owner name, class, TTL,
				and original TTL, are meaningless.  The class and TTL fields
				SHOULD be zero.  To conserve space, the owner name SHOULD be
				root (a single zero octet).  If transaction authentication is
				desired, that SIG RR must be considered the highest priority for
				inclusion.

		4.3 Processing Responses and SIG RRs

		   The following rules apply to the processing of SIG RRs included in a
		   response:

			 1. A security aware resolver that receives a response from a
				security aware server via a secure communication with the AD bit
				(see Section 6.1) set, MAY choose to accept the RRs as received
				without verifying the zone SIG RRs.

			 2. In other cases, a security aware resolver SHOULD verify the SIG
				RRs for the RRs of interest.  This may involve initiating
				additional queries for SIG or KEY RRs, especially in the case of
				getting a response from a server that does not implement
				security.  (As explained in 2.3.5 above, it will not be possible
				to secure CNAMEs being served up by non-secure resolvers.)

				NOTE: Implementers might expect the above SHOULD to be a MUST.
				However, local policy or the calling application may not require
				the security services.

			 3. If SIG RRs are received in response to a user query explicitly
				specifying the SIG type, no special processing is required.

		   If the message does not pass integrity checks or the SIG does not
		   check against the signed RRs, the SIG RR is invalid and should be
		   ignored.  If all of the SIG RR(s) purporting to authenticate an RRset
		   are invalid, then the RRset is not authenticated.

		   If the SIG RR is the last RR in a response in the additional
		   information section and has a type covered of zero, it is a
		   transaction signature of the response and the query that produced the
		   response.  It MAY be optionally checked and the message rejected if
		   the checks fail.  But even if the checks succeed, such a transaction
		   authentication SIG does NOT directly authenticate any RRs in the
		   message.  Only a proper SIG RR signed by the zone or a key tracing
		   its authority to the zone or to static resolver configuration can
		   directly authenticate RRs, depending on resolver policy (see Section
		   6).  If a resolver does not implement transaction and/or request
		   SIGs, it MUST ignore them without error.

		   If all checks indicate that the SIG RR is valid then RRs verified by
		   it should be considered authenticated.

		4.4 Signature Lifetime, Expiration, TTLs, and Validity

		   Security aware servers MUST NOT consider SIG RRs to authenticate
		   anything before their signature inception or after its expiration
		   time (see also Section 6).  Security aware servers MUST NOT consider
		   any RR to be authenticated after all its signatures have expired.
		   When a secure server caches authenticated data, if the TTL would
		   expire at a time further in the future than the authentication
		   expiration time, the server SHOULD trim the TTL in the cache entry
		   not to extent beyond the authentication expiration time.  Within
		   these constraints, servers should continue to follow DNS TTL aging.
		   Thus authoritative servers should continue to follow the zone refresh
		   and expire parameters and a non-authoritative server should count
		   down the TTL and discard RRs when the TTL is zero (even for a SIG
		   that has not yet reached its authentication expiration time).  In
		   addition, when RRs are transmitted in a query response, the TTL
		   should be trimmed so that current time plus the TTL does not extend
		   beyond the authentication expiration time.  Thus, in general, the TTL
		   on a transmitted RR would be

			  min(authExpTim,max(zoneMinTTL,min(originalTTL,currentTTL)))

		   When signatures are generated, signature expiration times should be
		   set far enough in the future that it is quite certain that new
		   signatures can be generated before the old ones expire.  However,
		   setting expiration too far into the future could mean a long time to
		   flush any bad data or signatures that may have been generated.

		   It is recommended that signature lifetime be a small multiple of the
		   TTL (ie, 4 to 16 times the TTL) but not less than a reasonable
		   maximum re-signing interval and not less than the zone expiry time.
    */

    [DNSField]
    public ushort TypeCovered { get; set; }

    [DNSField]
    public byte Algorithm { get; set; }

    [DNSField]
    public byte Labels { get; set; }

    [DNSField]
    public uint OriginalTtl { get; set; }

    [DNSField]
    public uint SignatureExpiration { get; set; }

    [DNSField]
    public uint SignatureInception { get; set; }

    [DNSField]
    public ushort KeyTag { get; set; }

    [DNSField]
    public string SignerName { get; set; }

    [DNSField]
    public byte[] Signature { get; set; }

}
