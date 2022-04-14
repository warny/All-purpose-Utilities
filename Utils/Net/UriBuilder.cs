using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Utils.Objects;

namespace Utils.Net
{
	public class UriBuilder
	{
		public static readonly IReadOnlyDictionary<string, int> DefaultPorts = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>{
			{Uri.UriSchemeFtp, 21},
			{Uri.UriSchemeGopher, 70},
			{Uri.UriSchemeHttp, 80},
			{Uri.UriSchemeHttps, 443},
			{Uri.UriSchemeNntp, 119},
			{ "nfs", 2049 },
			{ "telnet", 23 },
			{ "sftp", 22 },
			{ "ssh", 22 },
			{ "news", 144 },
			{ "smb", 445 },
			{ "nntps", 563 },
			{ "ftps", 990 }
		});

		public UriBuilder( Uri uri!! )
		{
			this.Scheme = uri.Scheme;
			this.Host = uri.Host;
			this.Port = uri.Port;
			this.AbsolutePath = uri.AbsolutePath;

			string[] userInfos = uri.UserInfo.Split(new[] { ':' }, 2);
			if (userInfos.Length >= 1) {
				this.Username = System.Web.HttpUtility.UrlDecode(userInfos[0]);
			}
			if (userInfos.Length >= 2) {
				this.Password = System.Web.HttpUtility.UrlDecode(userInfos[1]);
			}
			this.QueryString =  System.Web.HttpUtility.ParseQueryString(uri.Query);
			this.Fragment = uri.Fragment?.StartsWith("#") ?? false ? uri.Fragment.Substring(1) : uri.Fragment;
		}

		public UriBuilder( string uriString!! ) : this(new Uri(uriString)) {}


		public string Scheme { get; set; }
		public string Host { get; set; }
		public int Port { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public string AbsolutePath { get; set; }
		public string Fragment { get; set; }
		public NameValueCollection QueryString { get; set; }

		public override string ToString()
		{
			int port;
			if (DefaultPorts.TryGetValue(this.Scheme, out int defaultPort) && this.Port==defaultPort) {
				port = -1;
			} else {
				port =  this.Port;
			}

			System.UriBuilder builder = new System.UriBuilder(this.Scheme, this.Host, port, this.AbsolutePath);
			builder.UserName = Username;
			builder.Password = Password;
			var queryElements = QueryString
				.Cast<string>()
				.SelectMany(key => QueryString.GetValues(key).Select(value => $"{System.Web.HttpUtility.UrlEncode(key)}={System.Web.HttpUtility.UrlEncode(value)}"));

			builder.Query = string.Join("&", queryElements);
			builder.Fragment = Fragment.IsNullOrWhiteSpace() ? "" : "#" + Fragment;

			return builder.ToString();
		}
	}

}
