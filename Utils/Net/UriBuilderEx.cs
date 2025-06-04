using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Utils.Objects;

namespace Utils.Net;

/// <summary>
/// Provides a convenient way to construct and manipulate URIs, including user credentials, query parameters, and fragments.
/// </summary>
/// <remarks>
/// This class behaves similarly to <see cref="System.UriBuilder"/> but offers additional utility methods for:
/// <list type="bullet">
/// <item><description>Working with basic authorization (username/password).</description></item>
/// <item><description>Excluding credentials from the final URI when needed.</description></item>
/// <item><description>Auto-excluding well-known default ports from the URI.</description></item>
/// </list>
/// </remarks>
public class UriBuilderEx
{
	/// <summary>
	/// Maps URI schemes to their well-known default ports.
	/// Ports listed here are omitted from the final URI if they match the scheme's default value.
	/// </summary>
	/// <remarks>
	/// This dictionary includes common schemes such as <c>http</c>, <c>https</c>, and <c>ftp</c>, 
	/// alongside others like <c>ssh</c>, <c>sftp</c>, <c>nfs</c>, and <c>smb</c>.
	/// </remarks>
	public static readonly IReadOnlyDictionary<string, int> DefaultPorts = new ReadOnlyDictionary<string, int>(
		new Dictionary<string, int>
		{
			{ Uri.UriSchemeFtp, 21 },
			{ Uri.UriSchemeGopher, 70 },
			{ Uri.UriSchemeHttp, 80 },
			{ Uri.UriSchemeHttps, 443 },
			{ Uri.UriSchemeNntp, 119 },
			{ "nfs", 2049 },
			{ Uri.UriSchemeTelnet, 23 },
			{ Uri.UriSchemeSftp, 22 },
			{ Uri.UriSchemeSsh, 22 },
			{ Uri.UriSchemeNews, 144 },
			{ "smb", 445 },
			{ "nntps", 563 },
			{ Uri.UriSchemeFtps, 990 }
		}
	).ToImmutableDictionary();

	/// <summary>
	/// Initializes a new instance of the <see cref="UriBuilderEx"/> class based on a given <see cref="Uri"/> instance.
	/// </summary>
	/// <param name="uri">The <see cref="Uri"/> to parse and store.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
	public UriBuilderEx(Uri uri)
	{
		uri.Arg().MustNotBeNull();

		Scheme = uri.Scheme;
		Host = uri.Host;
		Port = uri.Port;
		AbsolutePath = uri.AbsolutePath;

		// Parse username and password from user info (if present).
                string[] userInfos = uri.UserInfo.Split([':'], 2);
		if (userInfos.Length >= 1)
		{
			Username = System.Web.HttpUtility.UrlDecode(userInfos[0]);
		}
		if (userInfos.Length >= 2)
		{
			Password = System.Web.HttpUtility.UrlDecode(userInfos[1]);
		}

		// Parse query string and fragment.
		QueryString = new QueryString(uri.Query);
		Fragment = uri.Fragment?.StartsWith("#") ?? false
			? uri.Fragment[1..]
			: uri.Fragment;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UriBuilderEx"/> class using a URI string.
	/// </summary>
	/// <param name="uriString">The string representation of a URI.</param>
	/// <exception cref="UriFormatException">Thrown if <paramref name="uriString"/> is not a valid URI.</exception>
	public UriBuilderEx(string uriString) : this(new Uri(uriString)) { }

	/// <summary>
	/// Gets or sets the URI scheme (e.g., http, https, ftp).
	/// </summary>
	public string Scheme { get; set; }

	/// <summary>
	/// Gets or sets the hostname or IP address for the URI.
	/// </summary>
	public string Host { get; set; }

	/// <summary>
	/// Gets or sets the port number. If it matches a known default port for the scheme, it may be excluded in the final URI.
	/// </summary>
	public int Port { get; set; }

	/// <summary>
	/// Gets or sets the username for basic authentication.
	/// </summary>
	public string Username { get; set; }

	/// <summary>
	/// Gets or sets the password for basic authentication.
	/// </summary>
	public string Password { get; set; }

	/// <summary>
	/// Gets or sets the absolute path part of the URI.
	/// </summary>
	public string AbsolutePath { get; set; }

	/// <summary>
	/// Gets or sets the fragment portion of the URI (the part after the '#').
	/// </summary>
	public string Fragment { get; set; }

	/// <summary>
	/// Gets or sets a collection representing the query parameters.
	/// </summary>
	/// <remarks>
	/// When building the final URL, each key/value pair is encoded and appended to the query string.
	/// </remarks>
	public QueryString QueryString { get; set; }

	/// <summary>
	/// Encodes and returns a Basic Authorization header value using <see cref="Username"/> and <see cref="Password"/>.
	/// </summary>
	/// <remarks>
	/// The format returned is a Base64-encoded string of "username:password". 
	/// Returns an empty string if both <see cref="Username"/> and <see cref="Password"/> are null or whitespace.
	/// </remarks>
	/// <returns>
	/// A Base64-encoded string representing basic authorization credentials, or an empty string if no credentials are present.
	/// </returns>
	public string GetBasicAuthorizationBase64String()
	{
		if (string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password))
		{
			return string.Empty;
		}

		var urlEncodedUsername = System.Web.HttpUtility.UrlEncode(Username);
		var urlEncodedPassword = System.Web.HttpUtility.UrlEncode(Password);
		return Convert.ToBase64String(
			Encoding.ASCII.GetBytes($"{urlEncodedUsername}:{urlEncodedPassword}")
		);
	}

	/// <summary>
	/// Builds and returns an internal <see cref="System.UriBuilder"/> instance with the current property values, excluding user credentials.
	/// </summary>
	/// <remarks>
	/// This method excludes user credentials because <see cref="System.UriBuilder"/> does not naturally handle them 
	/// without overwriting the <c>UriBuilder.UserName</c> and <c>UriBuilder.Password</c> properties later on.
	/// </remarks>
	/// <returns>A configured <see cref="System.UriBuilder"/> instance.</returns>
	private UriBuilder InnerBuildUrl()
	{
		int port;

		// If the current port matches the default port for the scheme, omit it in the final URI.
		if (DefaultPorts.TryGetValue(Scheme, out int defaultPort) && Port == defaultPort)
		{
			port = -1;
		}
		else
		{
			port = Port;
		}

		var builder = new UriBuilder(Scheme, Host, port, AbsolutePath)
		{
			// Convert query string collection into a valid query string.
			Query = string.Join("&", QueryString.ToString()),
			Fragment = string.IsNullOrWhiteSpace(Fragment) ? string.Empty : "#" + Fragment
		};

		return builder;
	}

	/// <summary>
	/// Returns the full URL including user credentials (if present).
	/// </summary>
	/// <remarks>
	/// The final URI includes the username and password within the <see cref="System.Uri.UserInfo"/> part of the URI.
	/// </remarks>
	/// <returns>A string representation of the full URL, including credentials if set.</returns>
	public string GetFullUrl()
	{
		var builder = InnerBuildUrl();

		builder.UserName = Username;
		builder.Password = Password;
		return builder.ToString();
	}

	/// <summary>
	/// Returns the URL without user credentials.
	/// </summary>
	/// <remarks>
	/// This method can be used when the caller intends to share or log the URI without exposing sensitive data.
	/// </remarks>
	/// <returns>A string representation of the URL, without credentials.</returns>
	public string GetUrlWithoutAuthorization()
	{
		var builder = InnerBuildUrl();
		return builder.ToString();
	}

	/// <inheritdoc />
	/// <summary>
	/// Returns a string representation of the URL, by default including authorization.
	/// </summary>
	/// <returns>A string representation of the URL, including credentials if present.</returns>
	public override string ToString() => ToString(string.Empty, null);

	/// <summary>
	/// Returns a string representation of the URL based on the specified format.
	/// </summary>
	/// <param name="format">A format specifier that indicates how to construct the URL. 
	/// Use <c>"s"</c> to exclude credentials, or any other value (including empty) to include credentials.</param>
	/// <returns>A string representation of the URL.</returns>
	public string ToString(string format) => ToString(format, null);

	/// <summary>
	/// Returns a string representation of the URL based on the specified format and culture information.
	/// </summary>
	/// <param name="format">
	/// A format specifier that indicates how to construct the URL.
	/// <list type="bullet">
	/// <item><description><c>"s"</c> to exclude credentials.</description></item>
	/// <item><description>Any other value to include credentials.</description></item>
	/// </list>
	/// </param>
	/// <param name="formatProvider">An optional provider used to format the output (currently ignored).</param>
	/// <returns>A string representation of the URL.</returns>
	public string ToString(string? format, IFormatProvider? formatProvider) 
		=> format switch
		{
			"s" => GetUrlWithoutAuthorization(),
			_ => GetFullUrl(),
		};

	/// <summary>
	/// Implicitly converts the specified <see cref="UriBuilderEx"/> to a <see cref="Uri"/>, excluding any authorization credentials.
	/// </summary>
	/// <param name="builder">The <see cref="UriBuilderEx"/> instance to convert.</param>
	/// <returns>A new <see cref="Uri"/> instance, excluding credentials.</returns>
	public static implicit operator Uri(UriBuilderEx builder) => new(builder.GetUrlWithoutAuthorization());
}
