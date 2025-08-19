using System;
using System.Diagnostics.CodeAnalysis;

namespace Utils.Net.DNS;

/// <summary>
/// Represents a DNS domain name composed of an optional subdomain and a parent domain.
/// Provides comparison and equality checks against both <see cref="string"/> and <see cref="DNSDomainName"/>.
/// </summary>
/// <remarks>
/// This struct splits the domain name into two parts: the immediate (leftmost) subdomain
/// and the optional remaining parent domain. For example, <c>sub.example.com</c> is split
/// into <c>SubDomain = "sub"</c> and <c>ParentDomain = "example.com"</c>.
/// It offers convenient methods and operators to facilitate domain concatenation, comparison,
/// and equality checks.
/// </remarks>
public readonly struct DNSDomainName :
	IEquatable<string>,
	IEquatable<DNSDomainName>,
	IComparable<string>,
	IComparable<DNSDomainName>,
	IComparable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DNSDomainName"/> struct with the specified domain name.
	/// </summary>
	/// <param name="value">A fully qualified domain name (e.g., "sub.example.com").</param>
	public DNSDomainName(string value)
	{
		Value = value;

		var split = value.Split('.', 2);
		SubDomain = split[0];
		ParentDomain = split.Length == 2 ? split[1] : null;
	}

	/// <summary>
	/// Gets the full domain name.
	/// </summary>
	public string Value { get; }

	/// <summary>
	/// Gets the leftmost subdomain (the portion before the first period).
	/// </summary>
	public string SubDomain { get; }

	/// <summary>
	/// Gets the remaining parent domain (the portion following the first period), or <c>null</c> if there is none.
	/// </summary>
	public string ParentDomain { get; }

	/// <summary>
	/// Implicitly converts a <see cref="DNSDomainName"/> to its <see cref="string"/> representation.
	/// </summary>
	/// <param name="dnsDomainName">The <see cref="DNSDomainName"/> to convert.</param>
	/// <returns>The <see cref="Value"/> of <paramref name="dnsDomainName"/>.</returns>
	public static implicit operator string(DNSDomainName dnsDomainName) => dnsDomainName.Value;

	/// <summary>
	/// Implicitly converts a <see cref="string"/> to a <see cref="DNSDomainName"/> instance.
	/// </summary>
	/// <param name="domainName">The <see cref="string"/> domain name.</param>
	/// <returns>A <see cref="DNSDomainName"/> representing <paramref name="domainName"/>.</returns>
	public static implicit operator DNSDomainName(string domainName) => new DNSDomainName(domainName);

	/// <summary>
	/// Appends another domain name or label to the current <see cref="DNSDomainName"/>.
	/// </summary>
	/// <param name="append">The domain name (or label) to append.</param>
	/// <returns>
	/// A new <see cref="DNSDomainName"/> composed of the current <see cref="Value"/> followed by the appended domain,
	/// separated by a dot.
	/// </returns>
	public DNSDomainName Append(string append)
		=> append is null ? this : new DNSDomainName(Value + "." + append);

	/// <summary>
	/// Appends another <see cref="DNSDomainName"/> to the current <see cref="DNSDomainName"/>.
	/// </summary>
	/// <param name="append">The <see cref="DNSDomainName"/> to append.</param>
	/// <returns>
	/// A new <see cref="DNSDomainName"/> composed of the current <see cref="Value"/> followed by the appended domain,
	/// separated by a dot.
	/// </returns>
	public DNSDomainName Append(DNSDomainName? append)
		=> append is null ? this : new DNSDomainName(Value + "." + append.Value);

	/// <summary>
	/// Returns the full domain name as a <see cref="string"/>.
	/// </summary>
	/// <returns>The domain name.</returns>
	public override string ToString() => Value;

	/// <inheritdoc />
	public int CompareTo([AllowNull] DNSDomainName other) => Value.CompareTo(other.Value);

	/// <inheritdoc />
	public int CompareTo([AllowNull] string other) => Value.CompareTo(other);

	/// <inheritdoc />
	public bool Equals([AllowNull] string other) => Value.Equals(other);

	/// <inheritdoc />
	public bool Equals([AllowNull] DNSDomainName other) => Value.Equals(other.Value);

	/// <inheritdoc />
	public override bool Equals(object obj) => obj switch
	{
		DNSDomainName dn => Equals(dn),
		string str => Equals(str),
		_ => false
	};

	/// <inheritdoc />
	public int CompareTo(object obj) => obj switch
	{
		DNSDomainName dn => CompareTo(dn),
		string str => CompareTo(str),
		_ => -1
	};

	/// <inheritdoc />
	public override int GetHashCode() => Value.GetHashCode();
}
