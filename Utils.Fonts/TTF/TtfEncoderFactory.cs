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
    /// (code page 10000) if available, otherwise ASCII. For Microsoft, most encoding IDs use
    /// Big-Endian Unicode (UTF-16BE), except the legacy CJK double-byte encodings (ShiftJIS, PRC,
    /// Big5, Wansung, Johab), which use their respective single/double-byte code pages. The Unicode
    /// platform always uses UTF-16BE.
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
                // Microsoft 'name' records: languageID (e.g. 1033 = en-US) is a Windows LCID, never
                // a .NET code page number, so it plays no part in encoding selection either way.
                TtfPlatFormId.Microsoft => GetMicrosoftEncoding(platformSpecificID),
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

    /// <summary>
    /// Selects the encoding for a Microsoft-platform 'name' record based on its encoding ID
    /// (<paramref name="platformSpecificID"/>). Per the OpenType spec, encoding IDs 0 (Symbol) and 1
    /// (Unicode BMP) use UTF-16BE, but the legacy double-byte CJK encoding IDs (2, 3, 4, 5, 6) use
    /// their own code pages instead -- these predate Microsoft's move to storing every 'name' record
    /// as UTF-16BE, and fonts with legacy CJK name records still rely on them.
    /// </summary>
    /// <param name="platformSpecificID">The Microsoft platform encoding ID.</param>
    private static Encoding GetMicrosoftEncoding(TtfPlatformSpecificID platformSpecificID) => (short)platformSpecificID switch
    {
        2 => Encoding.GetEncoding(932),  // ShiftJIS (Japanese)
        3 => Encoding.GetEncoding(936),  // PRC (Simplified Chinese, GBK)
        4 => Encoding.GetEncoding(950),  // Big5 (Traditional Chinese)
        5 => Encoding.GetEncoding(949),  // Wansung (Korean)
        6 => Encoding.GetEncoding(1361), // Johab (Korean)
        _ => Encoding.BigEndianUnicode,  // 0 = Symbol, 1 = Unicode BMP, 10 = Unicode full, ...
    };
}
