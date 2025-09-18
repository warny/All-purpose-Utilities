using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using Utils.Collections;
using Utils.Objects;

namespace Utils.Net;

/// <summary>
/// A collection class representing the key-value pairs in a query string,
/// allowing multiple values per key and intuitive syntax for manipulation.
/// </summary>
/// <example>
/// Given the query string: <c>?key1=val1&amp;key2=val2_1&amp;key2=val2_2</c>
/// <code>
/// var qs = new QueryString("key1=val1&amp;key2=val2_1&amp;key2=val2_2");
/// 
/// // Implicitly convert all values of "key1" to a comma-separated string ("val1")
/// string v1 = qs["key1"]; 
/// 
/// // For "key2", multiple values are returned as "val2_1, val2_2"
/// string v2 = qs["key2"];
/// 
/// // Access the first value of key2
/// string v2_1 = qs["key2"][0]; // "val2_1"
/// 
/// // Add a new value to key2
/// qs["key2"].Add("val2_3");
/// // The collection now represents key1=val1&amp;key2=val2_1&amp;key2=val2_2&amp;key2=val2_3
/// 
/// // Replace or set a brand-new key
/// qs["key3"] = new[] {"val3"}; 
/// // The collection now has key3=val3
/// 
/// // Overwrite key4 with multiple values
/// qs["key4"] = new[] { "val4_1", "val4_2" };
/// 
/// // Remove key4 entirely
/// qs.Remove("key4");
/// 
/// // Build the final query string
/// string finalQuery = qs.ToString();  // "key1=val1&amp;key2=val2_1&amp;key2=val2_2&amp;key2=val2_3&amp;key3=val3"
/// 
/// // Convert to NameValueCollection
/// NameValueCollection nameValue = (NameValueCollection)qs;
/// 
/// // Construct a new QueryString from NameValueCollection
/// var fromNvc = (QueryString)nameValue;
/// </code>
/// </example>
public class QueryString
{
	private readonly Dictionary<string, List<string>> _parameters;

	#region Constructors

	/// <summary>
	/// Initializes a new instance of the <see cref="QueryString"/> class with no initial parameters.
	/// </summary>
	public QueryString()
	{
		_parameters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="QueryString"/> class
	/// by parsing the provided query string.
	/// </summary>
	/// <param name="queryString">
	/// A raw query string (with or without a leading '?') such as "key1=val1&amp;key2=val2_1&amp;key2=val2_2".
	/// </param>
	public QueryString(string queryString) : this()
	{
		Parse(queryString);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="QueryString"/> class using an existing <see cref="NameValueCollection"/>.
	/// </summary>
	/// <param name="collection">An existing <see cref="NameValueCollection"/> instance.</param>
	public QueryString(NameValueCollection collection) : this()
	{
		if (collection == null) return;

		foreach (string? key in collection)
		{
			// 'key' can be null, skip if that's the case.
			if (key == null)
				continue;

			string[] values = collection.GetValues(key) ?? [];
			_parameters[key] = [.. values];
		}
	}

	#endregion

	#region Indexer and Accessors

	/// <summary>
	/// Gets or sets the collection of query values associated with a specific <paramref name="key"/>.
	/// </summary>
	/// <param name="key">The query parameter key.</param>
	/// <returns>
	/// A <see cref="QueryValues"/> object that can be implicitly converted to a comma-separated string,
	/// or iterated to access individual values.
	/// </returns>
	/// <remarks>
	/// If the key does not exist, a call to <c>get</c> will lazily create it with an empty list of values.
	/// Setting <c>null</c> or an empty array removes all instances of the key.
	/// </remarks>
	public QueryValues this[string key]
	{
		get {
			var values = _parameters.GetOrAdd(key, () => []);
			return new QueryValues(this, key, values);
		}

		set {
			if (value is null || value.Count == 0)
			{
				_parameters.Remove(key);
			}
			else
			{
				_parameters[key] = [.. value]; // Copy to preserve new list
			}
		}
	}

	/// <summary>
	/// Removes all values associated with the specified key.
	/// </summary>
	/// <param name="key">The query parameter key to remove.</param>
	/// <returns><see langword="true"/> if the key was found and removed; otherwise, <see langword="false"/>.</returns>
	public bool Remove(string key) => _parameters.Remove(key);

	/// <summary>
	/// Removes all keys and values from the collection.
	/// </summary>
	public void Clear() => _parameters.Clear();

	#endregion

	#region Parsing and Rendering

	/// <summary>
	/// Parses the given raw query string and loads it into this <see cref="QueryString"/>.
	/// </summary>
	/// <param name="queryString">A query string that may or may not include a leading '?'.</param>
	public void Parse(string queryString)
	{
		if (string.IsNullOrWhiteSpace(queryString))
		{
			return;
		}

		// Remove any leading '?' for safety.
		if (queryString[0] == '?')
		{
			queryString = queryString[1..];
		}

		// Split on '&'
		string[] pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
		foreach (var pair in pairs)
		{
			// Split on the first '='
			int equalIndex = pair.IndexOf('=');
			string key;
			string value;
			if (equalIndex < 0)
			{
				// There's no '=' sign, treat the whole thing as a key with empty value
				key = pair;
				value = string.Empty;
			}
			else
			{
				key = pair[..equalIndex];
				value = pair[(equalIndex + 1)..];
			}

			// URL Decode
			key = WebUtility.UrlDecode(key);
			value = WebUtility.UrlDecode(value);

			// Insert into dictionary
			if (!_parameters.ContainsKey(key))
			{
				_parameters[key] = [];
			}
			_parameters[key].Add(value);
		}
	}

	/// <summary>
	/// Builds and returns the query string from the current parameters.
	/// </summary>
	/// <returns>
	/// A URL-encoded query string in the form "key1=val1&amp;key2=val2_1&amp;key2=val2_2", 
	/// or an empty string if there are no parameters.
	/// </returns>
	public override string ToString()
	{
		if (_parameters.Count == 0)
		{
			return string.Empty;
		}

		var sb = new StringBuilder();
		bool first = true;
		foreach (var kvp in _parameters)
		{
			string encodedKey = WebUtility.UrlEncode(kvp.Key);

			foreach (var val in kvp.Value)
			{
				if (!first)
				{
					sb.Append('&');
				}
				else
				{
					first = false;
				}

				sb.Append(encodedKey);
				sb.Append('=');
				sb.Append(WebUtility.UrlEncode(val));
			}
		}
		return sb.ToString();
	}

	#endregion

	#region Conversion to/from NameValueCollection

	/// <summary>
	/// Creates and returns a <see cref="NameValueCollection"/> containing the parameters of this <see cref="QueryString"/>.
	/// </summary>
	/// <returns>A new <see cref="NameValueCollection"/> instance containing all keys and values.</returns>
	public NameValueCollection ToNameValueCollection()
	{
		var nvc = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
		foreach (var kvp in _parameters)
		{
			foreach (var val in kvp.Value)
			{
				nvc.Add(kvp.Key, val);
			}
		}
		return nvc;
	}

	/// <summary>
	/// Explicitly converts the current <see cref="QueryString"/> to a <see cref="NameValueCollection"/>.
	/// </summary>
	/// <param name="qs">The <see cref="QueryString"/> instance.</param>
	public static explicit operator NameValueCollection(QueryString qs)
	{
		return qs.ToNameValueCollection();
	}

	/// <summary>
	/// Explicitly converts a <see cref="NameValueCollection"/> to a <see cref="QueryString"/>.
	/// </summary>
	/// <param name="nvc">The <see cref="NameValueCollection"/> to convert.</param>
	public static explicit operator QueryString(NameValueCollection nvc)
	{
		return new QueryString(nvc);
	}

	#endregion

	#region Nested: QueryValues

	/// <summary>
	/// A helper class for working with multiple query values under a single key. 
	/// It can be used like a list or implicitly converted to a comma-separated string.
	/// </summary>
	public class QueryValues : 
		IList<string>,
		IEquatable<string>,
		IEquatable<IEnumerable<string>>,
		IEqualityOperators<QueryValues, string, bool>,
		IEqualityOperators<QueryValues, IEnumerable<string>, bool>
	{
		private readonly QueryString _parent;
		private readonly string _key;
		private readonly List<string> _values;

		/// <summary>
		/// Initializes a new instance of <see cref="QueryValues"/>.
		/// </summary>
		/// <param name="parent">The parent <see cref="QueryString"/>.</param>
		/// <param name="key">The query parameter key these values belong to.</param>
		/// <param name="values">The actual list of values for this key.</param>
		internal QueryValues(QueryString parent, string key, IEnumerable<string> values)
		{
			_parent = parent;
			_key = key;
			_values = values is List<string> l ? l : [..values];
		}

		/// <summary>
		/// Allows implicit conversion to a comma-separated string of all values.
		/// </summary>
		/// <param name="values">The <see cref="QueryValues"/> instance.</param>
		public static implicit operator string(QueryValues values)
		{
			return string.Join(",", values._values);
		}

		/// <summary>
		/// Allows assignment from a string array, overwriting existing values with the provided list.
		/// </summary>
		/// <param name="newValues">The set of new values to assign to this key.</param>
		public static implicit operator QueryValues?(string newValues)
		{
			// This operator is used indirectly by:
			//     myQueryString["key"] = new[] {"val1", "val2"};
			// The actual assignment logic is handled by the parent indexer set method, 
			// which checks for null/empty and replaces or removes the key.
			if (newValues == null || newValues.Length == 0)
			{
				return null;
			}

			return new QueryValues(null, null, [newValues]);
		}

		/// <summary>
		/// Allows assignment from a string array, overwriting existing values with the provided list.
		/// </summary>
		/// <param name="newValues">The set of new values to assign to this key.</param>
		public static implicit operator QueryValues?(List<string> newValues)
		{
			// This operator is used indirectly by:
			//     myQueryString["key"] = new[] {"val1", "val2"};
			// The actual assignment logic is handled by the parent indexer set method, 
			// which checks for null/empty and replaces or removes the key.
			if (newValues == null || newValues.Count == 0)
			{
				return null;
			}

			return new QueryValues(null, null, newValues);
		}

		/// <summary>
		/// Allows assignment from a string array, overwriting existing values with the provided list.
		/// </summary>
		/// <param name="newValues">The set of new values to assign to this key.</param>
		public static implicit operator QueryValues?(string[] newValues)
		{
			// This operator is used indirectly by:
			//     myQueryString["key"] = new[] {"val1", "val2"};
			// The actual assignment logic is handled by the parent indexer set method, 
			// which checks for null/empty and replaces or removes the key.
			if (newValues == null || newValues.Length == 0)
			{
				return null;
			}

			return new QueryValues(null, null, newValues);
		}

                /// <summary>
                /// Determines whether the query values match the provided string representation.
                /// </summary>
                /// <param name="left">The query value wrapper being compared.</param>
                /// <param name="right">The string representation to compare with.</param>
                /// <returns><see langword="true"/> when <paramref name="left"/> expands to <paramref name="right"/>.</returns>
                public static bool operator ==(QueryValues left, string right)
                        => left.Equals(right);

                /// <summary>
                /// Determines whether the query values differ from the provided string representation.
                /// </summary>
                /// <param name="left">The query value wrapper being compared.</param>
                /// <param name="right">The string representation to compare with.</param>
                /// <returns><see langword="true"/> when the values do not equal <paramref name="right"/>.</returns>
                public static bool operator !=(QueryValues left, string right)
                        => !left.Equals(right);

                /// <summary>
                /// Determines whether the query values are sequence-equal to the provided collection.
                /// </summary>
                /// <param name="left">The query value wrapper being compared.</param>
                /// <param name="right">The sequence of query values to compare with.</param>
                /// <returns><see langword="true"/> when all values are equal in order.</returns>
                public static bool operator ==(QueryValues left, IEnumerable<string> right)
                        => left.Equals(right);

                /// <summary>
                /// Determines whether the query values differ from the provided collection.
                /// </summary>
                /// <param name="left">The query value wrapper being compared.</param>
                /// <param name="right">The sequence of query values to compare with.</param>
                /// <returns><see langword="true"/> when the sequences contain different values.</returns>
                public static bool operator !=(QueryValues left, IEnumerable<string> right)
                        => !left.Equals(right);

		#region IList<string> Implementation

		/// <inheritdoc/>
		public int Count => _values.Count;

		/// <inheritdoc/>
		public bool IsReadOnly => false;

		/// <inheritdoc/>
		public string this[int index]
		{
			get => _values[index];
			set => _values[index] = value;
		}

		/// <inheritdoc/>
		public IEnumerator<string> GetEnumerator() => _values.GetEnumerator();

		/// <inheritdoc/>
		IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

		/// <inheritdoc/>
		public void Add(string item)
		{
			_parent._parameters[_key].Add(item);
		}

		/// <inheritdoc/>
		public void Clear() => _values.Clear();

		/// <inheritdoc/>
		public bool Contains(string item) => _values.Contains(item);

		/// <inheritdoc/>
		public void CopyTo(string[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);

		/// <inheritdoc/>
		public bool Remove(string item) => _values.Remove(item);

		/// <inheritdoc/>
		public int IndexOf(string item) => _values.IndexOf(item);

		/// <inheritdoc/>
		public void Insert(int index, string item) => _values.Insert(index, item);

		/// <inheritdoc/>
		public void RemoveAt(int index) => _values.RemoveAt(index);

                /// <inheritdoc/>
                public override bool Equals(object other)
                        => other switch
                        {
                                string str => Equals(str),
                                IEnumerable<string> strs => Equals(strs),
                                _ => false
                        };

                /// <inheritdoc/>
                public bool Equals(string other) => (string)this == other;

                /// <inheritdoc/>
                public bool Equals(IEnumerable<string> other)
                        => EnumerableEqualityComparer<string>.Default.Equals(this, other);

                /// <inheritdoc/>
                public override int GetHashCode() => ObjectUtils.ComputeHash(this._values);
		#endregion
	}

	#endregion
}
