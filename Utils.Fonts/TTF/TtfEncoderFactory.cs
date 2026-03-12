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
    /// Returns the appropriate <see cref="Encoding"/> based on the specified platform ID, platform-specific ID, and language ID.
    /// </summary>
    /// <param name="platformID">The TrueType platform ID.</param>
    /// <param name="platformSpecificID">The TrueType platform-specific ID.</param>
    /// <param name="languageID">The TrueType language ID.</param>
    /// <returns>
    /// The selected <see cref="Encoding"/>. For Macintosh, this returns MacRoman (code page 10000) if available;
    /// otherwise, it falls back to ASCII. For other platforms, Big‑Endian Unicode is used.
    /// </returns>
    public static Encoding GetEncoding(TtfPlatFormId platformID, TtfPlatformSpecificID platformSpecificID, TtfLanguageID languageID)
    {
        try
        {
            switch (platformID)
            {
                case TtfPlatFormId.Macintosh:
                    // MacRoman encoding (code page 10000) is the standard for Macintosh.
                    return Encoding.GetEncoding((int)languageID);
                case TtfPlatFormId.Microsoft:
                    return Encoding.GetEncoding((int)languageID);
                default:
                    // For platforms other than Macintosh, UTF-16 BE (BigEndianUnicode) is typically used.
                    return Encoding.BigEndianUnicode;
            }
        }
        catch
        {
            return Encoding.ASCII;
        }
    }
}
