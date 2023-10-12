using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Utils.Net.DNS
{
    public readonly struct DNSDomainName : 
        IEquatable<string>, 
        IEquatable<DNSDomainName>,
        IComparable<string>, 
        IComparable<DNSDomainName>,
        IComparable
    {
        public DNSDomainName(string value) {
            Value = value;
            var split = value.Split('.', 2);
            SubDomain = split[0];
            ParentDomain = split.Length == 2 ? split[1] : null;
        }

        public string Value { get; }
        public string SubDomain { get; }
        public string ParentDomain { get; }

        public static implicit operator string(DNSDomainName dnsDomainName) => dnsDomainName.Value;
        public static implicit operator DNSDomainName(string domainName) => new DNSDomainName(domainName);

        public DNSDomainName Append(string append) => append is null ? this : new DNSDomainName(this.Value + "." + append);
        public DNSDomainName Append(DNSDomainName? append) => append is null ? this : new DNSDomainName(this.Value + "." + append.Value);

        public override string ToString() => Value;

        public int CompareTo([AllowNull] DNSDomainName other) => Value.CompareTo(other.Value);
        public int CompareTo([AllowNull] string other) => Value.CompareTo(other);
        public bool Equals([AllowNull] string other) => Value.Equals(other);
        public bool Equals([AllowNull] DNSDomainName other) => Value.Equals(other.Value);

        public override bool Equals(object obj) => obj switch
        {
            DNSDomainName dn => Equals(dn),
            string str => Equals(str),
            _ => false
        };
        public int CompareTo(object obj) => obj switch
        {
            DNSDomainName dn => CompareTo(dn),
            string str => CompareTo(str),
            _ => -1
        };

        public override int GetHashCode() => Value.GetHashCode();
    }
}
