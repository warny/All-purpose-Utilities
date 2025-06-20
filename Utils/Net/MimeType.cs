using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;

namespace Utils.Net;

/// <summary>
/// Represents a MIME type with optional parameters.
/// </summary>
public class MimeType : IEquatable<MimeType>, IEqualityOperators<MimeType, MimeType, bool>
{
        /// <summary>
        /// Initializes a new instance of the <see cref="MimeType"/> class.
        /// </summary>
        /// <param name="type">The primary type, such as "text".</param>
        /// <param name="subType">The subtype, such as "plain".</param>
        /// <param name="parameters">Optional parameters associated with the MIME type.</param>
        public MimeType(string type, string subType, IDictionary<string, string>? parameters = null)
        {
                Type = type ?? throw new ArgumentNullException(nameof(type));
                SubType = subType ?? throw new ArgumentNullException(nameof(subType));
                Parameters = parameters != null
                        ? new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the primary type (e.g. "text" or "application").
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Gets the MIME subtype (e.g. "plain" or "json").
        /// </summary>
        public string SubType { get; }

        /// <summary>
        /// Gets the parameters for this MIME type.
        /// </summary>
        public IDictionary<string, string> Parameters { get; }

        /// <summary>
        /// Parses a MIME type string of the form
        /// "type/subtype; param1=value1; param2=value2".
        /// </summary>
        /// <param name="text">The textual representation.</param>
        /// <returns>A new <see cref="MimeType"/> instance.</returns>
        public static MimeType Parse(string text)
        {
                if (text == null) throw new ArgumentNullException(nameof(text));
                var parts = text.Split(';', 2, StringSplitOptions.TrimEntries);
                var types = parts[0].Split('/', 2, StringSplitOptions.TrimEntries);
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (parts.Length > 1)
                {
                        foreach (var p in parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                                var kv = p.Split('=', 2, StringSplitOptions.TrimEntries);
                                if (kv.Length == 2)
                                        parameters[kv[0]] = kv[1].Trim('"');
                        }
                }
                return new MimeType(types[0], types.Length > 1 ? types[1] : string.Empty, parameters);
        }

        /// <summary>
        /// Attempts to get the specified parameter value.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">When this method returns, contains the parameter value if found.</param>
        /// <returns><c>true</c> if the parameter exists; otherwise, <c>false</c>.</returns>
        public bool TryGetParameter(string name, out string? value)
                => Parameters.TryGetValue(name, out value);

        /// <summary>
        /// Sets a parameter value, replacing any existing value with the same name.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        public void SetParameter(string name, string value)
        {
                Parameters[name] = value;
        }

        /// <inheritdoc />
        public override string ToString()
        {
                var paramString = string.Join("; ", Parameters.Select(kv => $"{kv.Key}={kv.Value}"));
                return string.IsNullOrEmpty(paramString) ? $"{Type}/{SubType}" : $"{Type}/{SubType}; {paramString}";
        }

        /// <inheritdoc />
        public bool Equals(MimeType? other)
        {
                if (other == null) return false;
                if (!Type.Equals(other.Type, StringComparison.OrdinalIgnoreCase)) return false;
                if (!SubType.Equals(other.SubType, StringComparison.OrdinalIgnoreCase)) return false;
                return DictionaryEquals(Parameters, other.Parameters);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as MimeType);

        /// <inheritdoc />
        public override int GetHashCode()
        {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Type);
                hash = HashCode.Combine(hash, StringComparer.OrdinalIgnoreCase.GetHashCode(SubType));
                foreach (var kv in Parameters.OrderBy(k => k.Key))
                {
                        hash = HashCode.Combine(hash, StringComparer.OrdinalIgnoreCase.GetHashCode(kv.Key));
                        hash = HashCode.Combine(hash, StringComparer.OrdinalIgnoreCase.GetHashCode(kv.Value));
                }
                return hash;
        }

        /// <summary>
        /// Equality operator comparing two <see cref="MimeType"/> instances.
        /// </summary>
        public static bool operator ==(MimeType? left, MimeType? right) => left?.Equals(right) ?? right is null;

        /// <summary>
        /// Inequality operator comparing two <see cref="MimeType"/> instances.
        /// </summary>
        public static bool operator !=(MimeType? left, MimeType? right) => !(left == right);

        /// <summary>
        /// Determines whether the content of this MIME type can be represented by the specified <see cref="Type"/>.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <returns><c>true</c> if the MIME content can be mapped to the specified type; otherwise, <c>false</c>.</returns>
        public bool IsCompatibleWith(Type type)
        {
                return MimePartConverter.Default.CanConvertTo(type, this);
        }

        /// <summary>
        /// Determines whether the content of this MIME type can be represented by <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns><c>true</c> if compatible; otherwise, <c>false</c>.</returns>
        public bool IsCompatibleWith<T>() => MimePartConverter.Default.CanConvertTo<T>(this);

        private static bool DictionaryEquals(IDictionary<string, string> left, IDictionary<string, string> right)
        {
                if (left.Count != right.Count)
                        return false;
                foreach (var kv in left)
                {
                        if (!right.TryGetValue(kv.Key, out var value))
                                return false;
                        if (!kv.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                                return false;
                }
                return true;
        }

        /// <summary>
        /// Creates a <see cref="MimeType"/> instance for plain text.
        /// </summary>
        /// <param name="charset">Optional text encoding. Defaults to UTF-8.</param>
        /// <returns>A new <see cref="MimeType"/> representing <c>text/plain</c>.</returns>
        public static MimeType CreateTextPlain(string charset = "utf-8")
        {
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                        ["charset"] = charset
                };
                return new MimeType("text", "plain", parameters);
        }

        /// <summary>
        /// Creates a <see cref="MimeType"/> instance for JSON content.
        /// </summary>
        /// <param name="charset">Optional text encoding. Defaults to UTF-8.</param>
        /// <returns>A new <see cref="MimeType"/> representing <c>application/json</c>.</returns>
        public static MimeType CreateApplicationJson(string charset = "utf-8")
        {
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                        ["charset"] = charset
                };
                return new MimeType("application", "json", parameters);
        }

        /// <summary>
        /// Creates a <see cref="MimeType"/> instance for a multipart entity.
        /// </summary>
        /// <param name="subType">Multipart subtype (e.g. "mixed" or "form-data").</param>
        /// <param name="boundary">Boundary used to separate MIME parts.</param>
        /// <returns>A new <see cref="MimeType"/> representing <c>multipart/&lt;subType&gt;</c>.</returns>
        public static MimeType CreateMultipart(string subType, string boundary)
        {
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                        ["boundary"] = boundary
                };
                return new MimeType("multipart", subType, parameters);
        }
}
