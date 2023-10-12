using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Utils.Net.DNS
{
    public class DNSHeadersComparer : IEqualityComparer<DNSHeader>
    {
        private DNSElementsComparer comparer = DNSElementsComparer.Default;

        private DNSHeadersComparer() { }

        public static DNSHeadersComparer Default { get; } = new DNSHeadersComparer();

        public bool Equals([AllowNull] DNSHeader x, [AllowNull] DNSHeader y)
        {
            if (!comparer.Equals(x, y)) return false;
            for (int i = 0; i < x.Requests.Count; i++)
            {
                if (!comparer.Equals(x.Requests[i], y.Requests[i])) return false;
            }

            if (!AreEquals(x.Responses, y.Responses)) return false;
            if (!AreEquals(x.Authorities, y.Authorities)) return false;
            if (!AreEquals(x.Additionals, y.Additionals)) return false;
            return true;
        }

        private bool AreEquals(IList<DNSResponseRecord> record1, IList<DNSResponseRecord> record2)
        {
            if (record1.Count != record2.Count) return false;
            for (int i = 0; i < record1.Count; i++)
            {
                if (!comparer.Equals(record1[i], record2[i])) return false;
                if (!comparer.Equals(record1[i].RData, record2[i].RData)) return false;
            }
            return true;
        }

        public int GetHashCode([DisallowNull] DNSHeader obj)
        {
            int hashCode = 0;
            unchecked
            {
                hashCode = comparer.GetHashCode(obj);
                for (int i = 0; i < obj.Requests.Count; i++)
                {
                    hashCode = hashCode * 27 + comparer.GetHashCode(obj.Requests[i]);
                }
                hashCode = GetHashCode(hashCode, obj.Responses);
                hashCode = GetHashCode(hashCode, obj.Authorities);
                hashCode = GetHashCode(hashCode, obj.Additionals);
            }
            return hashCode;
        }

        private int GetHashCode(int hashCode, IList<DNSResponseRecord> record)
        {
            unchecked
            {

                for (int i = 0; i < record.Count; i++)
                {
                    hashCode = hashCode * 27 + comparer.GetHashCode(record[i]);
                    hashCode = hashCode * 27 + comparer.GetHashCode(record[i].RData);
                }
            }
            return hashCode;
        }
    }
}
