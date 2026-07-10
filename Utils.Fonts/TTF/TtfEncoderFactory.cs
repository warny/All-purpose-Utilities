using System;
using System.Text;
using Utils.Fonts.TTF.Tables;

namespace Utils.Fonts.TTF;

/// <summary>
/// A factory for selecting the appropriate text encoding for TrueType name table entries.
/// The encoding is determined by the triplet (PlatformId, PlatformSpecificId, LanguageId).
/// </summary>
public static class TtfEncoderFactory
{
    /// <summary>
    /// Returns the appropriate <see cref="Encoding"/> based on the specified platform ID and platform-specific ID.
    /// </summary>
    /// <param name="platformID">The TrueType platform ID.</param>
    /// <param name="platformSpecificID">The TrueType platform-specific ID.</param>
    /// <param name="languageID">
    /// The TrueType language ID. Per the OpenType 'name' table spec, the byte encoding of a record never
    /// depends on its language: this parameter only identifies which language the string is written in
    /// (e.g. English vs. French), not how its bytes are laid out. It is accepted for API symmetry with the
    /// documented (PlatformID, PlatformSpecificID, LanguageID) triplet but intentionally unused here.
    /// </param>
    /// <returns>
    /// The selected <see cref="Encoding"/>. For Macintosh with the Roman script, this returns MacRoman
    /// (code page 10000) if available, otherwise ASCII. For Microsoft and Unicode platforms, Big-Endian
    /// Unicode (UTF-16BE) is used, matching how those platforms always encode 'name' table strings.
    /// </returns>
    public static Encoding GetEncoding(TtfPlatFormId platformID, TtfPlatformSpecificID platformSpecificID, TtfLanguageID languageID)
    {
        try
        {
            return platformID switch
            {
                // Macintosh 'name' records are encoded per the script identified by
                // PlatformSpecificID (e.g. Roman -> MacRoman), never per language: languageID here
                // is a Macintosh *language* code (e.g. 0 = English), not a .NET code page number.
                TtfPlatFormId.Macintosh when platformSpecificID == TtfPlatformSpecificID.MAC_ROMAN
                    => Encoding.GetEncoding(10000), // MacRoman
                TtfPlatFormId.Macintosh => Encoding.ASCII,
                // Microsoft 'name' records are always UTF-16BE regardless of PlatformSpecificID
                // (Symbol / Unicode BMP / Unicode 2.0+): languageID here is a Windows LCID (e.g.
                // 1033 = en-US), not a .NET code page number either.
                TtfPlatFormId.Microsoft => Encoding.BigEndianUnicode,
                // The Unicode platform also always uses UTF-16BE.
                _ => Encoding.BigEndianUnicode,
            };
        }
        catch (ArgumentException)
        {
            return Encoding.ASCII;
        }
        catch (NotSupportedException)
        {
            return Encoding.ASCII;
        }
    }
}
