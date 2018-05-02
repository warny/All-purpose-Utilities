﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Web
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
		});

		public UriBuilder( Uri uri )
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
			this.Fragment = uri.Fragment;
		}

		public UriBuilder( string uriString ) : this(new Uri(uriString)) {}


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
			var queryElements = QueryString.Cast<string>()
				.Select(name => $"{System.Web.HttpUtility.UrlEncode(name)}={System.Web.HttpUtility.UrlEncode(QueryString[name])}");
			builder.Query = string.Join("&", queryElements);
			builder.Fragment  = Fragment;

			return builder.ToString();
		}
	}

}
