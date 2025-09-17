using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Objects;

namespace Utils.Net
{
	public static partial class MailUtils
	{
		/// <summary>
		/// A compiled <see cref="Regex"/> pattern for parsing a mail address of the form:
		/// <c>user@domain.com</c> or <c>Name &lt;user@domain.com&gt;</c>.
		/// </summary>
		/// <remarks>
		/// Culture is set to "fr-FR" for the pattern, but it generally applies to standard email addresses.
		/// </remarks>
		[GeneratedRegex("^(((?<name>[^;,<>\\n]+)\\s*)?\\<(?<mail>[^@<>;,\\s]+@(\\w+\\.)*\\w+)\\>|(?<mail>[^@<>;,\\s]+@(\\w+\\.)*\\w+))$",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline,
			"fr-FR")]
		private static partial Regex mailAddressParserRegex();

		private static readonly Regex mailAddressParser = mailAddressParserRegex();

		/// <summary>
		/// Parses a string into a <see cref="MailAddress"/> instance.
		/// The string may be of the form <c>user@domain.com</c> or <c>Name &lt;user@domain.com&gt;</c>.
		/// </summary>
		/// <param name="mailAddress">The string containing the mail address.</param>
		/// <returns>An instance of <see cref="MailAddress"/> representing the parsed address.</returns>
		/// <exception cref="ArgumentException">Thrown if the string does not match a valid mail address pattern.</exception>
		public static MailAddress ParseMailAddress(string mailAddress)
		{
			mailAddress.Arg().MustNotBeNull();

			var match = mailAddressParser.Match(mailAddress);
			if (!match.Success)
				throw new ArgumentException($"{mailAddress} is not a valid mail address", nameof(mailAddress));

			if (match.Groups["name"].Success)
				return new MailAddress(match.Groups["mail"].Value, match.Groups["name"].Value);

			return new MailAddress(match.Groups["mail"].Value);
		}

		/// <summary>
		/// Parses a string containing multiple mail addresses separated by 
		/// semicolons, commas, or newlines into an array of <see cref="MailAddress"/> objects.
		/// </summary>
		/// <param name="mailAddresses">A string with one or more email addresses.</param>
		/// <returns>An array of <see cref="MailAddress"/> objects.</returns>
		public static MailAddress[] ParseMailAddresses(string mailAddresses)
		{
			mailAddresses.Arg().MustNotBeNull();

			return mailAddresses
				.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s => ParseMailAddress(s.Trim(' ', '\t')))
				.ToArray();
		}

		/// <summary>
		/// Adds multiple <see cref="MailAddress"/> objects to this <see cref="MailAddressCollection"/>.
		/// </summary>
		/// <param name="addresses">The mail address collection to extend.</param>
		/// <param name="mailAddresses">An array of <see cref="MailAddress"/> objects to add.</param>
		public static void AddRange(this MailAddressCollection addresses, params MailAddress[] mailAddresses)
			=> addresses.AddRange((IEnumerable<MailAddress>)mailAddresses);

		/// <summary>
		/// Adds multiple <see cref="MailAddress"/> objects to this <see cref="MailAddressCollection"/>.
		/// </summary>
		/// <param name="addresses">The mail address collection to extend.</param>
		/// <param name="mailAddresses">An enumeration of <see cref="MailAddress"/> objects to add.</param>
		public static void AddRange(this MailAddressCollection addresses, IEnumerable<MailAddress> mailAddresses)
		{
			mailAddresses.Arg().MustNotBeNull();

			foreach (var mailAddress in mailAddresses)
			{
				addresses.Add(mailAddress);
			}
		}

		/// <summary>
		/// Adds multiple email addresses (as strings) to this <see cref="MailAddressCollection"/>.
		/// </summary>
		/// <param name="addresses">The mail address collection to extend.</param>
		/// <param name="mailAddresses">An array of email address strings.</param>
		public static void AddRange(this MailAddressCollection addresses, params string[] mailAddresses)
			=> addresses.AddRange((IEnumerable<string>)mailAddresses);

		/// <summary>
		/// Adds multiple email addresses (as strings) to this <see cref="MailAddressCollection"/>.
		/// </summary>
		/// <param name="addresses">The mail address collection to extend.</param>
		/// <param name="mailAddresses">An enumeration of email address strings.</param>
		public static void AddRange(this MailAddressCollection addresses, IEnumerable<string> mailAddresses)
		{
			mailAddresses.Arg().MustNotBeNull();

			addresses.AddRange(mailAddresses.Select(ParseMailAddress));
		}

		/// <summary>
		/// Adds one or more email addresses (given as a single string) to this <see cref="MailAddressCollection"/>.
		/// </summary>
		/// <param name="addresses">The mail address collection to extend.</param>
		/// <param name="mailAddresses">A string with one or more email addresses.</param>
		public static void AddRange(this MailAddressCollection addresses, string mailAddresses)
		{
			mailAddresses.Arg().MustNotBeNull();

			addresses.AddRange((IEnumerable<MailAddress>)ParseMailAddresses(mailAddresses));
		}

		/// <summary>
		/// Adds one or more recipients (To) to this <see cref="MailMessage"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to add addresses to.</param>
		/// <param name="mailAddresses">A string containing one or more email addresses.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage To(this MailMessage mailMessage, string mailAddresses)
		{
			mailMessage.Arg().MustNotBeNull();
			mailAddresses.Arg().MustNotBeNull();

			mailMessage.To.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Adds one or more recipients (To) to this <see cref="MailMessage"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to add addresses to.</param>
		/// <param name="mailAddresses">An array of email address strings.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage To(this MailMessage mailMessage, params string[] mailAddresses)
			=> mailMessage.To((IEnumerable<string>)mailAddresses);

		/// <summary>
		/// Adds one or more recipients (To) to this <see cref="MailMessage"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to add addresses to.</param>
		/// <param name="mailAddresses">An enumeration of email address strings.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage To(this MailMessage mailMessage, IEnumerable<string> mailAddresses)
		{
			mailMessage.Arg().MustNotBeNull();
			mailAddresses.Arg().MustNotBeNull();

			mailMessage.To.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Adds one or more CC (carbon copy) recipients to this <see cref="MailMessage"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to add CC addresses to.</param>
		/// <param name="mailAddresses">A string containing one or more email addresses.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage CC(this MailMessage mailMessage, string mailAddresses)
		{
			mailMessage.Arg().MustNotBeNull();
			mailAddresses.Arg().MustNotBeNull();

			mailMessage.CC.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Adds one or more CC (carbon copy) recipients to this <see cref="MailMessage"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to add CC addresses to.</param>
		/// <param name="mailAddresses">An array of email address strings.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage CC(this MailMessage mailMessage, params string[] mailAddresses)
			=> mailMessage.CC((IEnumerable<string>)mailAddresses);

		/// <summary>
		/// Adds one or more CC (carbon copy) recipients to this <see cref="MailMessage"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to add CC addresses to.</param>
		/// <param name="mailAddresses">An enumeration of email address strings.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage CC(this MailMessage mailMessage, IEnumerable<string> mailAddresses)
		{
			mailMessage.Arg().MustNotBeNull();
			mailAddresses.Arg().MustNotBeNull();

			mailMessage.CC.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Adds one or more BCC (blind carbon copy) recipients to this <see cref="MailMessage"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to add BCC addresses to.</param>
		/// <param name="mailAddresses">A string containing one or more email addresses.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage BCC(this MailMessage mailMessage, string mailAddresses)
		{
			mailMessage.Arg().MustNotBeNull();
			mailAddresses.Arg().MustNotBeNull();

			mailMessage.Bcc.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Adds one or more BCC (blind carbon copy) recipients to this <see cref="MailMessage"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to add BCC addresses to.</param>
		/// <param name="mailAddresses">An array of email address strings.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage BCC(this MailMessage mailMessage, params string[] mailAddresses)
			=> mailMessage.BCC((IEnumerable<string>)mailAddresses);

		/// <summary>
		/// Adds one or more BCC (blind carbon copy) recipients to this <see cref="MailMessage"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to add BCC addresses to.</param>
		/// <param name="mailAddresses">An enumeration of email address strings.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage BCC(this MailMessage mailMessage, IEnumerable<string> mailAddresses)
		{
			mailMessage.Arg().MustNotBeNull();
			mailAddresses.Arg().MustNotBeNull();

			mailMessage.Bcc.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Sets the subject of the mail message.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to modify.</param>
		/// <param name="subject">The subject text.</param>
		/// <param name="encoding">
		/// An optional <see cref="Encoding"/> for the subject. Defaults to <see cref="Encoding.UTF8"/>.
		/// </param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage Subject(this MailMessage mailMessage, string subject, Encoding encoding = null)
		{
			mailMessage.Arg().MustNotBeNull();
			subject.Arg().MustNotBeNull();

			mailMessage.Subject = subject;
			mailMessage.SubjectEncoding = encoding ?? Encoding.UTF8;
			return mailMessage;
		}

		/// <summary>
		/// Sets the body of the mail message. Automatically sets <see cref="MailMessage.IsBodyHtml"/>
		/// to <see langword="true"/> if the body starts with <c>&lt;html</c>, otherwise <see langword="false"/>.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to modify.</param>
		/// <param name="body">The body content of the message.</param>
		/// <param name="encoding">
		/// An optional <see cref="Encoding"/> for the body. Defaults to <see cref="Encoding.UTF8"/>.
		/// </param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage Body(this MailMessage mailMessage, string body, Encoding encoding = null)
		{
			mailMessage.Arg().MustNotBeNull();
			body.Arg().MustNotBeNull();

			mailMessage.Body = body;
			mailMessage.BodyEncoding = encoding ?? Encoding.UTF8;
			mailMessage.IsBodyHtml = body.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase);
			return mailMessage;
		}

		/// <summary>
		/// Attaches a file (by path) to the mail message.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to modify.</param>
		/// <param name="fileName">The file path to attach.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage Attachment(this MailMessage mailMessage, string fileName)
		{
			mailMessage.Arg().MustNotBeNull();
			fileName.Arg().MustNotBeNull();

			mailMessage.Attachments.Add(new Attachment(fileName));
			return mailMessage;
		}

		/// <summary>
		/// Attaches a <see cref="FileInfo"/> to the mail message.
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to modify.</param>
		/// <param name="fileInfo">The <see cref="FileInfo"/> representing the file to attach.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage Attachment(this MailMessage mailMessage, FileInfo fileInfo)
		{
			mailMessage.Arg().MustNotBeNull();
			fileInfo.Arg().MustNotBeNull();

			mailMessage.Attachments.Add(new Attachment(fileInfo.FullName));
			return mailMessage;
		}

		/// <summary>
		/// Attaches a stream to the mail message as a file. 
		/// <para>
		/// <strong>Note:</strong> The stream is used by the <see cref="Attachment(MailMessage, string)"/>.
		/// Do not dispose this stream until the <see cref="MailMessage"/> (and its attachments) 
		/// is disposed or the mail is sent.
		/// </para>
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to modify.</param>
		/// <param name="filename">The name of the file to represent the attached stream.</param>
		/// <param name="contentStream">A <see cref="Stream"/> containing the file content.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage Attachment(this MailMessage mailMessage, string filename, Stream contentStream)
		{
			mailMessage.Arg().MustNotBeNull();
			filename.Arg().MustNotBeNull();
			contentStream.Arg().MustNotBeNull();

			// Do not dispose the stream here. It needs to remain alive for the attachment.
			mailMessage.Attachments.Add(new Attachment(contentStream, filename));
			return mailMessage;
		}

		/// <summary>
		/// Attaches a byte array to the mail message as a file.
		/// <para>
		/// <strong>Note:</strong> The in-memory stream is kept alive by the <see cref="Attachment(MailMessage, string)"/>.
		/// It is disposed when the <see cref="MailMessage"/> or the <see cref="Attachment(MailMessage, string)"/> is disposed.
		/// </para>
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to modify.</param>
		/// <param name="filename">The name of the file to represent the attached byte array.</param>
		/// <param name="content">A byte array containing the file content.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage Attachment(this MailMessage mailMessage, string filename, byte[] content)
		{
			mailMessage.Arg().MustNotBeNull();
			filename.Arg().MustNotBeNull();
			content.Arg().MustNotBeNull();

			// Keep stream open for attachment usage.
			var contentStream = new MemoryStream(content)
			{
				Position = 0
			};
			mailMessage.Attachments.Add(new Attachment(contentStream, filename));
			return mailMessage;
		}

		/// <summary>
		/// Attaches a string as a file to the mail message.
		/// <para>
		/// <strong>Note:</strong> The in-memory stream is kept alive by the <see cref="Attachment(MailMessage, string)"/>.
		/// It is disposed when the <see cref="MailMessage"/> or the <see cref="Attachment(MailMessage, string)"/> is disposed.
		/// </para>
		/// </summary>
		/// <param name="mailMessage">The <see cref="MailMessage"/> object to modify.</param>
		/// <param name="filename">The file name for the attached content.</param>
		/// <param name="content">The string content to attach.</param>
		/// <param name="encoding">An optional encoding. Defaults to <see cref="Encoding.UTF8"/>.</param>
		/// <returns>The updated <see cref="MailMessage"/> instance for chaining.</returns>
		public static MailMessage Attachment(this MailMessage mailMessage, string filename, string content, Encoding encoding = null)
		{
			mailMessage.Arg().MustNotBeNull();
			filename.Arg().MustNotBeNull();
			content.Arg().MustNotBeNull();

			encoding ??= Encoding.UTF8;

			// Keep stream open for attachment usage.
			var contentStream = new MemoryStream();
			using (var writer = new StreamWriter(contentStream, encoding, leaveOpen: true))
			{
				writer.Write(content);
			}
			contentStream.Position = 0;

			mailMessage.Attachments.Add(new Attachment(contentStream, filename));
			return mailMessage;
		}
	}
}
