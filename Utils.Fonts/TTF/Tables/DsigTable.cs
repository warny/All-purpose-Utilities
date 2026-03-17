using System;
using System.IO;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'DSIG' (Digital Signature) table contains one or more cryptographic signatures that
/// attest to the authenticity and integrity of a font. Most tools treat this table as an opaque
/// blob: they preserve it on read and discard or regenerate it on write. This implementation
/// stores the entire table as raw bytes after the 12-byte outer header.
/// </summary>
/// <remarks>
/// <para>Header structure (12 bytes):</para>
/// <list type="table">
///   <item><term>UInt32 version</term><description>Always 1.</description></item>
///   <item><term>UInt16 numSigs</term><description>Number of signature blocks.</description></item>
///   <item><term>UInt16 flags</term><description>Signing flags (usually 1 = cannot be modified).</description></item>
/// </list>
/// <para>Followed by <c>numSigs</c> SignatureRecord structs and then the signature data blobs.</para>
/// </remarks>
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/dsig"/>
[TTFTable(TableTypes.Tags.DSIG)]
public class DsigTable : TrueTypeTable
{
    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="DsigTable"/> class.</summary>
    public DsigTable() : base(TableTypes.DSIG) { }

    // ── Public properties ─────────────────────────────────────────────────

    /// <summary>Gets or sets the DSIG version (always 1).</summary>
    public uint DsigVersion { get; set; } = 1;

    /// <summary>Gets or sets the number of signature blocks.</summary>
    public ushort NumSigs { get; set; }

    /// <summary>Gets or sets the signing flags (bit 0 = cannot be modified after signing).</summary>
    public ushort Flags { get; set; }

    /// <summary>
    /// Gets or sets the raw bytes of all signature records and their data blobs,
    /// as they appear in the table immediately after the 8-byte header.
    /// This is kept opaque because regenerating valid signatures is not in scope.
    /// </summary>
    public byte[] SignatureData { get; set; } = [];

    // ── Length ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override int Length => 8 + SignatureData.Length;

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        DsigVersion = data.Read<UInt32>();
        NumSigs     = data.Read<UInt16>();
        Flags       = data.Read<UInt16>();

        // Read remaining bytes as opaque blob
        int remaining = (int)data.BytesLeft;
        SignatureData = new byte[remaining];
        for (int i = 0; i < remaining; i++)
            SignatureData[i] = (byte)data.ReadByte();
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        data.Write<UInt32>(DsigVersion);
        data.Write<UInt16>(NumSigs);
        data.Write<UInt16>(Flags);
        foreach (byte b in SignatureData)
            data.WriteByte(b);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    DsigVersion : {DsigVersion}");
        sb.AppendLine($"    NumSigs     : {NumSigs}");
        sb.AppendLine($"    Flags       : {Flags}");
        sb.AppendLine($"    DataBytes   : {SignatureData.Length}");
        return sb.ToString();
    }
}
