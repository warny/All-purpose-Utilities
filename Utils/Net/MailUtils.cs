using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Utils.Net
{
	public static class MailUtils
	{
		private static readonly Regex mailAddressParser = new Regex(@"^(((?<name>[^;,<>\n]+)\s*)?\<(?<mail>[^@<>;,\s]+@(\w+\.)*\w+)\>|(?<mail>[^@<>;,\s]+@(\w+\.)*\w+))$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.ExplicitCapture);


		public static MailAddress ParseMailAddress(string mailAddress)
		{
			var m = mailAddressParser.Match(mailAddress);
			if (!m.Success) throw new ArgumentException($"{mailAddress} n'est pas une adresse valide", nameof(mailAddress));
			if (m.Groups["name"].Success) return new MailAddress(m.Groups["mail"].Value, m.Groups["name"].Value);
			return new MailAddress(m.Groups["mail"].Value);
		}

		public static MailAddress[] ParseMailAddresses(string mailAddresses)
		{
			return mailAddresses
				.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s=> ParseMailAddress(s.Trim(' ', '\t')))
				.ToArray();
		}

		public static void AddRange(this MailAddressCollection addresses, params MailAddress[] mailAddresses)
			=> AddRange(addresses, (IEnumerable<MailAddress>)mailAddresses);

		public static void AddRange(this MailAddressCollection addresses, IEnumerable<MailAddress> mailAddresses)
		{
			foreach (var mailAddress in mailAddresses)
			{
				addresses.Add(mailAddress);
			}
		}

		public static void AddRange(this MailAddressCollection addresses, params string[] mailAddresses)
			=> AddRange(addresses, (IEnumerable<string>)mailAddresses);

		public static void AddRange(this MailAddressCollection addresses, IEnumerable<string> mailAddresses)
			=> AddRange(addresses, mailAddresses.Select(ma=>ParseMailAddress(ma)));

		public static void AddRange(this MailAddressCollection addresses, string mailAddresses)
			=> AddRange(addresses, (IEnumerable<MailAddress>)ParseMailAddresses(mailAddresses));

		public static MailMessage To(this MailMessage mailMessage, string mailAdresses)
		{
			mailMessage.To.AddRange(mailAdresses);
			return mailMessage;
		}

		public static MailMessage CC(this MailMessage mailMessage, string mailAdresses)
		{
			mailMessage.CC.AddRange(mailAdresses);
			return mailMessage;
		}

		public static MailMessage BCC(this MailMessage mailMessage, string mailAdresses)
		{
			mailMessage.Bcc.AddRange(mailAdresses);
			return mailMessage;
		}

		public static MailMessage Subject(this MailMessage mailMessage, string subject, Encoding encoding = null)
		{
			mailMessage.Subject = subject;
			mailMessage.SubjectEncoding = encoding ?? Encoding.UTF8;
			return mailMessage;
		}

		public static MailMessage Body(this MailMessage mailMessage, string body, Encoding encoding = null)
		{
			mailMessage.Body = body;
			mailMessage.BodyEncoding = encoding ?? Encoding.UTF8;
			mailMessage.IsBodyHtml = body.TrimStart().StartsWith("<html");
			return mailMessage;
		}

		public static MailMessage Attachement(this MailMessage mailMessage, string file)
		{
			mailMessage.Attachments.Add(new Attachment(file));
			return mailMessage;
		}

		public static MailMessage Attachement(this MailMessage mailMessage, string filename, Stream contentStream)
		{
			mailMessage.Attachments.Add(new Attachment(contentStream, filename));
			return mailMessage;
		}

		public static MailMessage Attachement(this MailMessage mailMessage, string filename, byte[] content)
		{
			using (MemoryStream contentStream = new MemoryStream(content)) {
				contentStream.Position = 0;
				mailMessage.Attachments.Add(new Attachment(contentStream, filename));
			}
			return mailMessage;
		}

		public static MailMessage Attachement(this MailMessage mailMessage, string filename, string content, Encoding encoding = null)
		{
			encoding ??= Encoding.UTF8;
			using (MemoryStream contentStream = new MemoryStream())
			{
				StreamWriter sw = new StreamWriter(contentStream, encoding);
				sw.Write(content);
				sw.Flush();
				contentStream.Position = 0;
				mailMessage.Attachments.Add(new Attachment(contentStream, filename));
			}
			return mailMessage;
		}
	}
}
