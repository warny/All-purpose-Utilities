using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Utils.Objects;

namespace Utils.Net
{
	public static partial class MailUtils
	{

        [GeneratedRegex("^(((?<name>[^;,<>\\n]+)\\s*)?\\<(?<mail>[^@<>;,\\s]+@(\\w+\\.)*\\w+)\\>|(?<mail>[^@<>;,\\s]+@(\\w+\\.)*\\w+))$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline, "fr-FR")]
        private static partial Regex mailAddressParserRegex();
        
		private static readonly Regex mailAddressParser = mailAddressParserRegex();

		/// <summary>
		/// Transforme une chaîne en adresse mail
		/// </summary>
		/// <param name="mailAddress">Chaîne décrivant une adresse de la forme "boite@domaine.com" ou "boite@domaine.com &lt;Nom du recipiendaire&qt;"</param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
		public static MailAddress ParseMailAddress(string mailAddress)
		{
			mailAddress.ArgMustNotBeNull();
			var m = mailAddressParser.Match(mailAddress);
			if (!m.Success) throw new ArgumentException($"{mailAddress} n'est pas une adresse valide", nameof(mailAddress));
			if (m.Groups["name"].Success) return new MailAddress(m.Groups["mail"].Value, m.Groups["name"].Value);
			return new MailAddress(m.Groups["mail"].Value);
		}

		/// <summary>
		/// Transforme une chaine contenant plusieurs adresse mail en un tableau d'adresses
		/// </summary>
		/// <param name="mailAddresses"></param>
		/// <returns></returns>
		public static MailAddress[] ParseMailAddresses(string mailAddresses)
		{
			mailAddresses.ArgMustNotBeNull();
			return mailAddresses
				.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s=> ParseMailAddress(s.Trim(' ', '\t')))
				.ToArray();
		}

		/// <summary>
		/// Ajoute un tableau contenant des adresses à une liste d'adresses
		/// </summary>
		/// <param name="addresses"></param>
		/// <param name="mailAddresses"></param>
		public static void AddRange(this MailAddressCollection addresses, params MailAddress[] mailAddresses)
			=> addresses.AddRange((IEnumerable<MailAddress>)mailAddresses);

		/// <summary>
		/// Ajoute une énumration d'adresses à une liste d'adresses
		/// </summary>
		/// <param name="addresses"></param>
		/// <param name="mailAddresses"></param>
		public static void AddRange(this MailAddressCollection addresses, IEnumerable<MailAddress> mailAddresses)
		{
			mailAddresses.ArgMustNotBeNull();

			foreach (var mailAddress in mailAddresses)
			{
				addresses.Add(mailAddress);
			}
		}

		/// <summary>
		/// Ajoute un tableau d'adresses sous forme de chaînes à une liste d'adresses
		/// </summary>
		/// <param name="addresses"></param>
		/// <param name="mailAddresses"></param>
		public static void AddRange(this MailAddressCollection addresses, params string[] mailAddresses)
			=> addresses.AddRange((IEnumerable<string>)mailAddresses);

		/// <summary>
		/// Ajoute une énumration d'adresses sous forme de chaînes à une liste d'adresses
		/// </summary>
		/// <param name="addresses"></param>
		/// <param name="mailAddresses"></param>
		public static void AddRange(this MailAddressCollection addresses, IEnumerable<string> mailAddresses) {
			mailAddresses.ArgMustNotBeNull();

			addresses.AddRange(mailAddresses.Select(ma => ParseMailAddress(ma)));
		}

		/// <summary>
		/// Ajoute une liste d'adresses sous forme d'une chaîne à une liste d'adresses
		/// </summary>
		/// <param name="addresses"></param>
		/// <param name="mailAddresses"></param>
		public static void AddRange(this MailAddressCollection addresses, string mailAddresses) {
			mailAddresses.ArgMustNotBeNull();
			addresses.AddRange((IEnumerable<MailAddress>)ParseMailAddresses(mailAddresses));
		}

		/// <summary>
		/// Ajoute une ou des adresses en destinataire
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="mailAdresses">Adresses à ajouter</param>
		/// <returns></returns>
		public static MailMessage To(this MailMessage mailMessage, string mailAddresses)
		{
			mailMessage.ArgMustNotBeNull();
			mailAddresses.ArgMustNotBeNull();
			mailMessage.To.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Ajoute une ou des adresses en destinataire
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="mailAdresses">Adresses à ajouter</param>
		/// <returns></returns>
		public static MailMessage To(this MailMessage mailMessage, params string[] mailAdresses) => mailMessage.To((IEnumerable<string>)mailAdresses);

		/// <summary>
		/// Ajoute une ou des adresses en destinataire
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="mailAdresses">Adresses à ajouter</param>
		/// <returns></returns>
		public static MailMessage To(this MailMessage mailMessage, IEnumerable<string> mailAddresses)
		{
			mailMessage.ArgMustNotBeNull();
			mailAddresses.ArgMustNotBeNull();

			mailMessage.To.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Ajoute une ou des adresses en copie 
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="mailAdresses">Adresses à ajouter</param>
		/// <returns></returns>
		public static MailMessage CC(this MailMessage mailMessage, string mailAddresses)
		{
			mailMessage.ArgMustNotBeNull();
			mailAddresses.ArgMustNotBeNull();
			mailMessage.CC.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Ajoute une ou des adresses en copie 
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="mailAdresses">Adresses à ajouter</param>
		/// <returns></returns>
		public static MailMessage CC(this MailMessage mailMessage, params string[] mailAdresses) => mailMessage.CC((IEnumerable<string>) mailAdresses);

		/// <summary>
		/// Ajoute une ou des adresses en copie 
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="mailAdresses">Adresses à ajouter</param>
		/// <returns></returns>
		public static MailMessage CC(this MailMessage mailMessage, IEnumerable<string> mailAddresses)
		{
			mailMessage.ArgMustNotBeNull();
			mailAddresses.ArgMustNotBeNull();
			mailMessage.CC.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Ajoute une ou des adresses en copie cachée
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="mailAdresses">Adresses à ajouter</param>
		/// <returns></returns>
		public static MailMessage BCC(this MailMessage mailMessage, string mailAddresses)
		{
			mailMessage.ArgMustNotBeNull();
			mailAddresses.ArgMustNotBeNull();
			mailMessage.Bcc.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Ajoute une ou des adresses en copie cachée
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="mailAdresses">Adresses à ajouter</param>
		/// <returns></returns>
		public static MailMessage BCC(this MailMessage mailMessage, params string[] mailAddresses) => mailMessage.BCC((IEnumerable<string>)mailAddresses);

		/// <summary>
		/// Ajoute une ou des adresses en copie cachée
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="mailAdresses">Adresses à ajouter</param>
		/// <returns></returns>
		public static MailMessage BCC(this MailMessage mailMessage, IEnumerable<string> mailAddresses)
		{
			mailMessage.ArgMustNotBeNull();
			mailAddresses.ArgMustNotBeNull();
			mailMessage.Bcc.AddRange(mailAddresses);
			return mailMessage;
		}

		/// <summary>
		/// Ecrit le sujet du message 
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="body">Sujet du message</param>
		/// <param name="encoding">Encodage du sujet (par défaut <see cref="Encoding.UTF8"/>)</param>
		/// <returns></returns>
		public static MailMessage Subject(this MailMessage mailMessage, string subject, Encoding encoding = null)
		{
			mailMessage.ArgMustNotBeNull();
			subject.ArgMustNotBeNull();

			mailMessage.Subject = subject;
			mailMessage.SubjectEncoding = encoding ?? Encoding.UTF8;
			return mailMessage;
		}

		/// <summary>
		/// Ecrit le corps du message 
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="body">Corps de message</param>
		/// <param name="encoding">Encodage du corps (par défaut <see cref="Encoding.UTF8"/>)</param>
		/// <returns></returns>
		public static MailMessage Body(this MailMessage mailMessage, string body, Encoding encoding = null)
		{
			mailMessage.ArgMustNotBeNull();
			body.ArgMustNotBeNull();
 
			mailMessage.Body = body;
			mailMessage.BodyEncoding = encoding ?? Encoding.UTF8;
			mailMessage.IsBodyHtml = body.TrimStart().StartsWith("<html");
			return mailMessage;
		}

		/// <summary>
		/// Envoi le fichier en tant que fichier joint
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="fileName">Fichier à envoyer en pièce jointe</param>
		/// <returns></returns>
		public static MailMessage Attachment(this MailMessage mailMessage, string fileName)
		{
			mailMessage.ArgMustNotBeNull();
			fileName.ArgMustNotBeNull();
			mailMessage.Attachments.Add(new Attachment(fileName));
			return mailMessage;
		}

		/// <summary>
		/// Envoi le fichier en tant que fichier joint
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="fileInfo">Fichier à envoyer en pièce jointe</param>
		/// <returns></returns>
		public static MailMessage Attachment(this MailMessage mailMessage, FileInfo fileInfo)
		{
			mailMessage.Attachments.Add(new Attachment(fileInfo.FullName));
			return mailMessage;
		}
		
		/// <summary>
		/// Ecrit un flux en tant que fichier joint
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="filename">Nom du chier joint</param>
		/// <param name="contentStream">Contenu du fichier</param>
		/// <returns></returns>
		public static MailMessage Attachement(this MailMessage mailMessage, string filename, Stream contentStream)
		{
			mailMessage.ArgMustNotBeNull();
			filename.ArgMustNotBeNull();
			contentStream.ArgMustNotBeNull();
			mailMessage.Attachments.Add(new Attachment(contentStream, filename));
			return mailMessage;
		}

		/// <summary>
		/// Ecrit un tableau d'octet en tant que fichier joint
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="filename">Nom du fichier joint</param>
		/// <param name="content">Contenu du fichier</param>
		/// <returns></returns>
		public static MailMessage Attachment(this MailMessage mailMessage, string filename, byte[] content)
		{
			mailMessage.ArgMustNotBeNull();
			filename.ArgMustNotBeNull();
			content.ArgMustNotBeNull();
			using (MemoryStream contentStream = new MemoryStream(content)) {
				contentStream.Position = 0;
				mailMessage.Attachments.Add(new Attachment(contentStream, filename));
			}
			return mailMessage;
		}

		/// <summary>
		/// Ecrit une chaîne en tant que fichier joint
		/// </summary>
		/// <param name="mailMessage">Message</param>
		/// <param name="filename">Nom du fichier joint</param>
		/// <param name="content">Contenu du fichier</param>
		/// <param name="encoding">Encodage (par défaut <see cref="Encoding.UTF8"/>)</param>
		/// <returns></returns>
		public static MailMessage Attachment(this MailMessage mailMessage, string filename, string content, Encoding encoding = null)
		{
			mailMessage.ArgMustNotBeNull();
			filename.ArgMustNotBeNull();
			content.ArgMustNotBeNull();
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
