using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Utils.Objects;

namespace Utils.Net
{
	public class UriBuilderEx
	{
		public static readonly IReadOnlyDictionary<string, int> DefaultPorts = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>{
			{Uri.UriSchemeFtp, 21},
			{Uri.UriSchemeGopher, 70},
			{Uri.UriSchemeHttp, 80},
			{Uri.UriSchemeHttps, 443},
			{Uri.UriSchemeNntp, 119},
			{ "nfs", 2049 },
			{ Uri.UriSchemeTelnet, 23 },
			{ Uri.UriSchemeSftp, 22 },
			{ Uri.UriSchemeSsh, 22 },
			{ Uri.UriSchemeNews, 144 },
			{ "smb", 445 },
			{ "nntps", 563 },
			{ Uri.UriSchemeFtps, 990 }
		}).ToImmutableDictionary();

		public UriBuilderEx(Uri uri)
		{
			uri.Arg().MustNotBeNull();

			this.Scheme = uri.Scheme;
			this.Host = uri.Host;
			this.Port = uri.Port;
			this.AbsolutePath = uri.AbsolutePath;

			string[] userInfos = uri.UserInfo.Split(new[] { ':' }, 2);
			if (userInfos.Length >= 1)
			{
				this.Username = System.Web.HttpUtility.UrlDecode(userInfos[0]);
			}
			if (userInfos.Length >= 2)
			{
				this.Password = System.Web.HttpUtility.UrlDecode(userInfos[1]);
			}
			this.QueryString = System.Web.HttpUtility.ParseQueryString(uri.Query);
			this.Fragment = uri.Fragment?.StartsWith("#") ?? false ? uri.Fragment.Substring(1) : uri.Fragment;
		}

		public UriBuilderEx(string uriString) : this(new Uri(uriString)) { }


		public string Scheme { get; set; }
		public string Host { get; set; }
		public int Port { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public string AbsolutePath { get; set; }
		public string Fragment { get; set; }
		public NameValueCollection QueryString { get; set; }

		public string GetBasicAuthorizationBase64String()
		{
			if (string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password)) return "";
			var urlEncodedUsername = System.Web.HttpUtility.UrlEncode(Username);
			var urlEncodedPassword = System.Web.HttpUtility.UrlEncode(this.Password);
			return Convert.ToBase64String(Encoding.ASCII.GetBytes($"{urlEncodedUsername}:{urlEncodedPassword}"));
		}

		private UriBuilder InnerBuildUrl()
		{
			int port;
			if (DefaultPorts.TryGetValue(this.Scheme, out int defaultPort) && this.Port == defaultPort)
			{
				port = -1;
			}
			else
			{
				port = this.Port;
			}

			UriBuilder builder = new UriBuilder(this.Scheme, this.Host, port, this.AbsolutePath);
			var queryElements = QueryString
				.Cast<string>()
				.SelectMany(key => QueryString.GetValues(key).Select(value => $"{System.Web.HttpUtility.UrlEncode(key)}={System.Web.HttpUtility.UrlEncode(value)}"));

			builder.Query = string.Join("&", queryElements);
			builder.Fragment = string.IsNullOrWhiteSpace(Fragment) ? "" : "#" + Fragment;
			return builder;
		}

		public string GetFullUrl()
		{
			UriBuilder builder = InnerBuildUrl();

			builder.UserName = Username;
			builder.Password = Password;
			return builder.ToString();
		}

		public string GetUrlWithoutAuthorization()
		{
			UriBuilder builder = InnerBuildUrl();
			return builder.ToString();
		}

		public override string ToString() => ToString("", null);
		public string ToString(string format) => ToString(format, null);

		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			switch (format)
			{
				case "s":
					return GetUrlWithoutAuthorization();
				default:
					return GetFullUrl();
			}
		}

		public static implicit operator Uri(UriBuilderEx builder) => new Uri(builder.GetUrlWithoutAuthorization());

	}

}
