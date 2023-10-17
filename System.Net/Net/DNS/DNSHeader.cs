using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Utils.Net;

namespace Utils.Net.DNS
{
	public class DNSHeader : DNSElement
	{
        /*
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            | 0| 1| 2| 3| 4| 5| 6| 7| 8| 9|10|11|12|13|14|15|
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                      ID                       |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			|QR|   Opcode  |AA|TC|RD|RA| Z|AD|CD|   RCODE   |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    QDCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    ANCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    NSCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    ARCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

         
		Addition FROM RFC 2535
		6. How to Resolve Securely and the AD and CD Bits

		   Retrieving or resolving secure data from the Domain Name System (DNS)
		   involves starting with one or more trusted public keys that have been
		   staticly configured at the resolver.  With starting trusted keys, a
		   resolver willing to perform cryptography can progress securely
		   through the secure DNS structure to the zone of interest as described
		   in Section 6.3. Such trusted public keys would normally be configured
		   in a manner similar to that described in Section 6.2.  However, as a
		   practical matter, a security aware resolver would still gain some
		   confidence in the results it returns even if it was not configured
		   with any keys but trusted what it got from a local well known server
		   as if it were staticly configured.

		   Data stored at a security aware server needs to be internally
		   categorized as Authenticated, Pending, or Insecure. There is also a
		   fourth transient state of Bad which indicates that all SIG checks
		   have explicitly failed on the data. Such Bad data is not retained at
		   a security aware server. Authenticated means that the data has a
		   valid SIG under a KEY traceable via a chain of zero or more SIG and
		   KEY RRs allowed by the resolvers policies to a KEY staticly
		   configured at the resolver. Pending data has no authenticated SIGs
		   and at least one additional SIG the resolver is still trying to
		   authenticate.  Insecure data is data which it is known can never be
		   either Authenticated or found Bad in the zone where it was found
		   because it is in or has been reached via a unsecured zone or because
		   it is unsigned glue address or delegation point NS data. Behavior in
		   terms of control of and flagging based on such data labels is
		   described in Section 6.1.

		   The proper validation of signatures requires a reasonably secure
		   shared opinion of the absolute time between resolvers and servers as
		   described in Section 6.4.

		6.1 The AD and CD Header Bits

		   Two previously unused bits are allocated out of the DNS
		   query/response format header. The AD (authentic data) bit indicates
		   in a response that all the data included in the answer and authority
		   portion of the response has been authenticated by the server
		   according to the policies of that server. The CD (checking disabled)
		   bit indicates in a query that Pending (non-authenticated) data is
		   acceptable to the resolver sending the query.
		   These bits are allocated from the previously must-be-zero Z field as
		   follows:

												   1  1  1  1  1  1
					 0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
					+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
					|                      ID                       |
					+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
					|QR|   Opcode  |AA|TC|RD|RA| Z|AD|CD|   RCODE   |
					+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
					|                    QDCOUNT                    |
					+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
					|                    ANCOUNT                    |
					+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
					|                    NSCOUNT                    |
					+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
					|                    ARCOUNT                    |
					+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

		   These bits are zero in old servers and resolvers.  Thus the responses
		   of old servers are not flagged as authenticated to security aware
		   resolvers and queries from non-security aware resolvers do not assert
		   the checking disabled bit and thus will be answered by security aware
		   servers only with Authenticated or Insecure data. Security aware
		   resolvers MUST NOT trust the AD bit unless they trust the server they
		   are talking to and either have a secure path to it or use DNS
		   transaction security.

		   Any security aware resolver willing to do cryptography SHOULD assert
		   the CD bit on all queries to permit it to impose its own policies and
		   to reduce DNS latency time by allowing security aware servers to
		   answer with Pending data.

		   Security aware servers MUST NOT return Bad data.  For non-security
		   aware resolvers or security aware resolvers requesting service by
		   having the CD bit clear, security aware servers MUST return only
		   Authenticated or Insecure data in the answer and authority sections
		   with the AD bit set in the response. Security aware servers SHOULD
		   return Pending data, with the AD bit clear in the response, to
		   security aware resolvers requesting this service by asserting the CD
		   bit in their request.  The AD bit MUST NOT be set on a response
		   unless all of the RRs in the answer and authority sections of the
		   response are either Authenticated or Insecure.  The AD bit does not
		   cover the additional information section.

		6.2 Staticly Configured Keys

		   The public key to authenticate a zone SHOULD be defined in local
		   configuration files before that zone is loaded at the primary server
		   so the zone can be authenticated.

		   While it might seem logical for everyone to start with a public key
		   associated with the root zone and staticly configure this in every
		   resolver, this has problems.  The logistics of updating every DNS
		   resolver in the world should this key ever change would be severe.
		   Furthermore, many organizations will explicitly wish their "interior"
		   DNS implementations to completely trust only their own DNS servers.
		   Interior resolvers of such organizations can then go through the
		   organization's zone servers to access data outside the organization's
		   domain and need not be configured with keys above the organization's
		   DNS apex.

		   Host resolvers that are not part of a larger organization may be
		   configured with a key for the domain of their local ISP whose
		   recursive secure DNS caching server they use.

		6.3 Chaining Through The DNS

		   Starting with one or more trusted keys for any zone, it should be
		   possible to retrieve signed keys for that zone's subzones which have
		   a key. A secure sub-zone is indicated by a KEY RR with non-null key
		   information appearing with the NS RRs in the sub-zone and which may
		   also be present in the parent.  These make it possible to descend
		   within the tree of zones.

		6.3.1 Chaining Through KEYs

		   In general, some RRset that you wish to validate in the secure DNS
		   will be signed by one or more SIG RRs.  Each of these SIG RRs has a
		   signer under whose name is stored the public KEY to use in
		   authenticating the SIG.  Each of those KEYs will, generally, also be
		   signed with a SIG.  And those SIGs will have signer names also
		   referring to KEYs.  And so on. As a result, authentication leads to
		   chains of alternating SIG and KEY RRs with the first SIG signing the
		   original data whose authenticity is to be shown and the final KEY
		   being some trusted key staticly configured at the resolver performing
		   the authentication.

		   In testing such a chain, the validity periods of the SIGs encountered
		   must be intersected to determine the validity period of the
		   authentication of the data, a purely algorithmic process. In
		   addition, the validation of each SIG over the data with reference to
		   a KEY must meet the objective cryptographic test implied by the
		   cryptographic algorithm used (although even here the resolver may
		   have policies as to trusted algorithms and key lengths).  Finally,
		   the judgement that a SIG with a particular signer name can
		   authenticate data (possibly a KEY RRset) with a particular owner
		   name, is primarily a policy question.  Ultimately, this is a policy
		   local to the resolver and any clients that depend on that resolver's
		   decisions.  It is, however, recommended, that the policy below be
		   adopted:

				Let A < B mean that A is a shorter domain name than B formed by
				dropping one or more whole labels from the left end of B, i.e.,
				A is a direct or indirect superdomain of B.  Let A = B mean that
				A and B are the same domain name (i.e., are identical after
				letter case canonicalization).  Let A > B mean that A is a
				longer domain name than B formed by adding one or more whole
				labels on the left end of B, i.e., A is a direct or indirect
				subdomain of B

				Let Static be the owner names of the set of staticly configured
				trusted keys at a resolver.

				Then Signer is a valid signer name for a SIG authenticating an
				RRset (possibly a KEY RRset) with owner name Owner at the
				resolver if any of the following three rules apply:

				(1) Owner > or = Signer (except that if Signer is root, Owner
				must be root or a top level domain name).  That is, Owner is the
				same as or a subdomain of Signer.

				(2) ( Owner < Signer ) and ( Signer > or = some Static ).  That
				is, Owner is a superdomain of Signer and Signer is staticly
				configured or a subdomain of a staticly configured key.

				(3) Signer = some Static.  That is, the signer is exactly some
				staticly configured key.

		   Rule 1 is the rule for descending the DNS tree and includes a special
		   prohibition on the root zone key due to the restriction that the root
		   zone be only one label deep.  This is the most fundamental rule.

		   Rule 2 is the rule for ascending the DNS tree from one or more
		   staticly configured keys.  Rule 2 has no effect if only root zone
		   keys are staticly configured.

		   Rule 3 is a rule permitting direct cross certification.  Rule 3 has
		   no effect if only root zone keys are staticly configured.

		   Great care should be taken that the consequences have been fully
		   considered before making any local policy adjustments to these rules
		   (other than dispensing with rules 2 and 3 if only root zone keys are
		   staticly configured).

		6.3.2 Conflicting Data

		   It is possible that there will be multiple SIG-KEY chains that appear
		   to authenticate conflicting RRset answers to the same query.  A
		   resolver should choose only the most reliable answer to return and
		   discard other data.  This choice of most reliable is a matter of
		   local policy which could take into account differing trust in
		   algorithms, key sizes, staticly configured keys, zones traversed,
		   etc.  The technique given below is recommended for taking into
		   account SIG-KEY chain length.

		   A resolver should keep track of the number of successive secure zones
		   traversed from a staticly configured key starting point to any secure
		   zone it can reach.  In general, the lower such a distance number is,
		   the greater the confidence in the data.  Staticly configured data
		   should be given a distance number of zero.  If a query encounters
		   different Authenticated data for the same query with different
		   distance values, that with a larger value should be ignored unless
		   some other local policy covers the case.

		   A security conscious resolver should completely refuse to step from a
		   secure zone into a unsecured zone unless the unsecured zone is
		   certified to be non-secure by the presence of an authenticated KEY RR
		   for the unsecured zone with the no-key type value.  Otherwise the
		   resolver is getting bogus or spoofed data.

		   If legitimate unsecured zones are encountered in traversing the DNS
		   tree, then no zone can be trusted as secure that can be reached only
		   via information from such non-secure zones. Since the unsecured zone
		   data could have been spoofed, the "secure" zone reached via it could
		   be counterfeit.  The "distance" to data in such zones or zones
		   reached via such zones could be set to 256 or more as this exceeds
		   the largest possible distance through secure zones in the DNS.

		6.4 Secure Time

		   Coordinated interpretation of the time fields in SIG RRs requires
		   that reasonably consistent time be available to the hosts
		   implementing the DNS security extensions.

		   A variety of time synchronization protocols exist including the
		   Network Time Protocol (NTP [RFC 1305, 2030]).  If such protocols are
		   used, they MUST be used securely so that time can not be spoofed.
		   Otherwise, for example, a host could get its clock turned back and
		   might then believe old SIG RRs, and the data they authenticate, which
		   were valid but are no longer.
         
         
         */

        public DNSHeader() {
            ID = (ushort)new Random().Next();
            QrBit = DNSQRBit.Question;
        }

        [DNSField]
        public ushort ID { get; set; }
        [DNSField]
        internal ushort Flags { get; set; }
        [DNSField]
        internal ushort QDCount { get; set; }
        [DNSField]
        internal ushort ANCount { get; set; }
        [DNSField]
        internal ushort NSCount { get; set; }
        [DNSField]
        internal ushort ARCount { get; set; }

        public IList<DNSRequestRecord> Requests { get; } = new List<DNSRequestRecord>();
        public IList<DNSResponseRecord> Responses { get; } = new List<DNSResponseRecord>();
        public IList<DNSResponseRecord> Authorities { get; } = new List<DNSResponseRecord>();
        public IList<DNSResponseRecord> Additionals { get; } = new List<DNSResponseRecord>();

        public DNSQRBit QrBit
        {
            get => (DNSQRBit)(Flags & DNSConstants.QR);
            set => Flags = (ushort)((Flags & ~DNSConstants.QR) | (ushort)value);
        }
        public DNSOpCode OpCode
        {
            get => (DNSOpCode)(Flags & DNSConstants.OpCode);
            set => Flags = (ushort)((Flags & ~DNSConstants.OpCode) | (ushort)value);
        }

        public bool AuthoritativeAnswer
        {
            get => (Flags & DNSConstants.AuthoritativeAnswer) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.AuthoritativeAnswer) | (value ? DNSConstants.AuthoritativeAnswer : 0));
        }

        public bool MessageTruncated
        {
            get => (Flags & DNSConstants.MessageTruncated) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.MessageTruncated) | (value ? DNSConstants.MessageTruncated : 0));
        }

        public bool RecursionDesired 
        {
            get => (Flags & DNSConstants.RecursionDesired) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.RecursionDesired) | (value ? DNSConstants.RecursionDesired : 0));
        }
        public bool RecursionPossible
        {
            get => (Flags & DNSConstants.RecursionPossible) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.RecursionPossible) | (value ? DNSConstants.RecursionPossible : 0));
        }

		public bool AuthenticDatas
		{
            get => (Flags & DNSConstants.AuthenticDatas) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.AuthenticDatas) | (value ? DNSConstants.AuthenticDatas : 0));
        }

        public bool CheckingDisabled
        {
            get => (Flags & DNSConstants.CheckingDisabled) != 0;
            set => Flags = (ushort)((Flags & ~DNSConstants.CheckingDisabled) | (value ? DNSConstants.CheckingDisabled : 0));
        }

        public byte ReservedFlags
        {
            get => (byte)(Flags & DNSConstants.ReservedZ);
            set => Flags = (ushort)((Flags | ~DNSConstants.ReservedZ) & (value & DNSConstants.ReservedZ));
        }

        public DNSError ErrorCode
        {
            get => (DNSError)((ushort)Flags & DNSConstants.Error);
            set => Flags = (ushort)((Flags | ~DNSConstants.Error) & ((ushort)value & DNSConstants.Error));
        }

		public void Append(DNSHeader header)
		{
			if (this.ID == header.ID) { throw new Exception("Mismatch headers"); }
			foreach (var request in header.Requests)
			{
				if (!Requests.Contains(request, DNSElementsComparer.Default))
				{
					Requests.Add((DNSRequestRecord)request.Clone());
				}
			}
			foreach (var response in header.Responses)
			{
				if (!Responses.Contains(response))
				{
					Responses.Add((DNSResponseRecord)response.Clone());
				}
			}
            foreach (var authority in header.Authorities)
            {
                if (!Authorities.Contains(authority))
                {
                    Authorities.Add((DNSResponseRecord)authority.Clone());
                }
            }
            foreach (var additional in header.Additionals)
            {
                if (!Additionals.Contains(additional))
                {
                    Additionals.Add((DNSResponseRecord)additional.Clone());
                }
            }
        }

		public override string ToString() =>
			$""""
			{QrBit} ID = {ID}, Operation Code = {OpCode} 
				Recursition possible = {RecursionPossible}, Recursion desired = {RecursionDesired}
				Authentic Datas = {AuthenticDatas}, Checking Disables {CheckingDisabled}
			Requests :
				{String.Join(Environment.NewLine + "\t", Requests.Select(r=>r.ToString().Replace(Environment.NewLine, Environment.NewLine + "\t")))}
			Responses : 
				{String.Join(Environment.NewLine + "\t", Responses.Select(r => r.ToString().Replace(Environment.NewLine, Environment.NewLine + "\t")))}
			Authorities : 
				{String.Join(Environment.NewLine + "\t", Authorities.Select(r => r.ToString().Replace(Environment.NewLine, Environment.NewLine + "\t")))}
			Additionals : 
				{String.Join(Environment.NewLine + "\t", Additionals.Select(r => r.ToString().Replace(Environment.NewLine, Environment.NewLine + "\t")))}
			"""";
    }
}
