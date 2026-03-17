using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF.Tables;

/// <summary>
/// The 'feat' (Feature Name) table lists the Apple Advanced Typography (AAT) layout features
/// supported by the font and their available settings. Each feature has a numeric identifier,
/// a name-table entry, and one or more named setting values.
/// </summary>
/// <see href="https://developer.apple.com/fonts/TrueType-Reference-Manual/RM06/Chap6feat.html"/>
[TTFTable(TableTypes.Tags.FEAT)]
public class FeatTable : TrueTypeTable
{
    // ── Nested types ──────────────────────────────────────────────────────

    /// <summary>One named setting for a feature (e.g. "On", "Off", "Standard Ligatures").</summary>
    public sealed class FeatureSetting
    {
        /// <summary>Gets or sets the setting identifier (feature-specific meaning).</summary>
        public ushort Setting { get; set; }

        /// <summary>Gets or sets the name-table ID for the human-readable setting name.</summary>
        public short NameIndex { get; set; }
    }

    /// <summary>
    /// Describes one AAT layout feature, its flags, its name-table entry, and its available settings.
    /// </summary>
    public sealed class FeatureNameRecord
    {
        /// <summary>Gets or sets the feature code (e.g. 1 = All Caps, 3 = Ligatures, 14 = Smart Swash).</summary>
        public ushort Feature { get; set; }

        /// <summary>Gets or sets the feature flags (bit 15 = exclusive, bit 14 = has default).</summary>
        public ushort FeatureFlags { get; set; }

        /// <summary>Gets or sets the name-table ID for the feature name.</summary>
        public short NameIndex { get; set; }

        /// <summary>Gets or sets the settings available for this feature.</summary>
        public FeatureSetting[] Settings { get; set; } = [];
    }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new instance of the <see cref="FeatTable"/> class.</summary>
    public FeatTable() : base(TableTypes.FEAT) { }

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Gets or sets the table version (0x00010000 = version 1.0).</summary>
    public int Version { get; set; } = 0x00010000;

    /// <summary>Gets or sets the array of feature name records.</summary>
    public FeatureNameRecord[] Features { get; set; } = [];

    // ── Length ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override int Length
    {
        get
        {
            // Header: Fixed(4) + featureNameCount(2) + reserved(2) + reserved(4) = 12 bytes
            // Each FeatureNameRecord on disk: feature(2)+nSettings(2)+offset(4)+flags(2)+nameIndex(2) = 12 bytes
            // Each FeatureSetting on disk: setting(2)+nameIndex(2) = 4 bytes
            int size = 12 + Features.Length * 12;
            foreach (var f in Features)
                size += f.Settings.Length * 4;
            return size;
        }
    }

    // ── ReadData ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void ReadData(Reader data)
    {
        Version = data.Read<Int32>();
        int featureCount = data.Read<UInt16>();
        data.Read<UInt16>(); // reserved
        data.Read<UInt32>(); // reserved

        // Read feature name records (12 bytes each): we need the offset to read settings later
        var records = new (ushort feature, ushort nSettings, uint offset, ushort flags, short nameIndex)[featureCount];
        for (int i = 0; i < featureCount; i++)
        {
            records[i] = (
                data.Read<UInt16>(),   // feature
                data.Read<UInt16>(),   // nSettings
                data.Read<UInt32>(),   // settingTableOffset (from table start)
                data.Read<UInt16>(),   // featureFlags
                data.Read<Int16>()     // nameIndex
            );
        }

        // Build FeatureNameRecord objects; settings are at absolute offsets from table start
        Features = new FeatureNameRecord[featureCount];
        for (int i = 0; i < featureCount; i++)
        {
            var (feature, nSettings, offset, flags, nameIndex) = records[i];
            data.Push((int)offset, SeekOrigin.Begin);
            var settings = new FeatureSetting[nSettings];
            for (int s = 0; s < nSettings; s++)
            {
                settings[s] = new FeatureSetting
                {
                    Setting   = data.Read<UInt16>(),
                    NameIndex = data.Read<Int16>(),
                };
            }
            data.Pop();

            Features[i] = new FeatureNameRecord
            {
                Feature      = feature,
                FeatureFlags = flags,
                NameIndex    = nameIndex,
                Settings     = settings,
            };
        }
    }

    // ── WriteData ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void WriteData(Writer data)
    {
        // Header (12 bytes)
        data.Write<Int32>(Version);
        data.Write<UInt16>((ushort)Features.Length);
        data.Write<UInt16>(0);  // reserved
        data.Write<UInt32>(0);  // reserved

        // Compute absolute offsets for each feature's settings table.
        // Header = 12, feature records = Features.Length × 12
        int settingsBase = 12 + Features.Length * 12;
        var settingOffsets = new int[Features.Length];
        int cursor = settingsBase;
        for (int i = 0; i < Features.Length; i++)
        {
            settingOffsets[i] = cursor;
            cursor += Features[i].Settings.Length * 4;
        }

        // Write feature name records
        for (int i = 0; i < Features.Length; i++)
        {
            var f = Features[i];
            data.Write<UInt16>(f.Feature);
            data.Write<UInt16>((ushort)f.Settings.Length);
            data.Write<UInt32>((uint)settingOffsets[i]);
            data.Write<UInt16>(f.FeatureFlags);
            data.Write<Int16>(f.NameIndex);
        }

        // Write settings tables
        foreach (var f in Features)
        {
            foreach (var s in f.Settings)
            {
                data.Write<UInt16>(s.Setting);
                data.Write<Int16>(s.NameIndex);
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    Version      : {Version:X8}");
        sb.AppendLine($"    FeatureCount : {Features.Length}");
        foreach (var f in Features)
            sb.AppendLine($"      Feature {f.Feature}: nameID={f.NameIndex}, settings={f.Settings.Length}");
        return sb.ToString();
    }
}
