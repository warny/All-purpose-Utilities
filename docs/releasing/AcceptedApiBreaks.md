# Accepted API breaks for 2.0.0-rc.1

This human-review index summarizes the exact machine-enforced diagnostics in `eng/api-baselines/accepted-api-breaks.json`. The JSON file remains authoritative; every addition or stale acceptance fails the API gate.

<a id="omy-utils"></a>
## omy.Utils

- Published baseline: `1.2.1`
- Accepted diagnostics: **114**
- Diagnostic classes: `CP0001`: 70, `CP0002`: 34, `CP0006`: 3, `CP0008`: 2, `CP0009`: 1, `CP0021`: 4

### Removed or incompatible published surface

- `CP0002` — `System.Collections.Generic.IEnumerable<T> Utils.Collections.EnumerableEx.FollowedBy<T>(this System.Collections.Generic.IEnumerable<T>, params T[])`
- `CP0002` — `System.Collections.Generic.IEnumerable<T> Utils.Collections.EnumerableEx.PrecededBy<T>(this System.Collections.Generic.IEnumerable<T>, params T[])`
- `CP0002` — `System.Collections.Generic.IEnumerable<System.Collections.Generic.IEnumerable<T>> Utils.Collections.EnumerableEx.Slice<T>(this System.Collections.Generic.IEnumerable<T>, params int[])`
- `CP0001` — `Utils.Collections.SkipList<T>`
- `CP0001` — `Utils.Collections.SymbolTree`
- `CP0001` — `Utils.Collections.SymbolLeaf`
- `CP0001` — `Utils.Expressions.ExpressionParser`
- `CP0001` — `Utils.Expressions.ExpressionParserCore`
- `CP0001` — `Utils.Expressions.IBuilder`
- `CP0001` — `Utils.Expressions.TryReadToken`
- `CP0001` — `Utils.Expressions.StringTransformer`
- `CP0001` — `Utils.Expressions.IStartExpressionBuilder`
- `CP0001` — `Utils.Expressions.IFollowUpExpressionBuilder`
- `CP0001` — `Utils.Expressions.IAdditionalTokens`
- `CP0002` — `void Utils.Expressions.LiteralPart.Append(string)`
- `CP0001` — `Utils.Expressions.IParserOptions`
- `CP0001` — `Utils.Expressions.ParserContext`
- `CP0001` — `Utils.Expressions.Tokenizer`
- `CP0001` — `Utils.Expressions.ITokenizerPosition`
- `CP0001` — `Utils.Format.StringFormat`
- `CP0001` — `Utils.Mathematics.INumberToStringConverter`
- `CP0002` — `System.Nullable<T> Utils.Mathematics.NullableIntEx.Min<T>(System.Nullable<T>, System.Nullable<T>, bool)`
- `CP0002` — `System.Nullable<T> Utils.Mathematics.NullableIntEx.Max<T>(System.Nullable<T>, System.Nullable<T>, bool)`
- `CP0002` — `bool Utils.Mathematics.NullableIntEx.GreaterOrEqual<T>(System.Nullable<T>, System.Nullable<T>)`
- `CP0002` — `int Utils.Mathematics.NullableIntEx.Compare<T>(System.Nullable<T>, System.Nullable<T>, bool)`
- `CP0002` — `bool Utils.Mathematics.FloatingPointComparer<T>.Equals(T, T)`
- `CP0002` — `int Utils.Mathematics.FloatingPointComparer<T>.GetHashCode(T)`
- `CP0001` — `Utils.Mathematics.Numbers`
- `CP0001` — `Utils.Mathematics.NumberType`
- `CP0001` — `Utils.Mathematics.NumberListType`
- `CP0001` — `Utils.Mathematics.DigitType`
- `CP0001` — `Utils.Mathematics.DigitListType`
- `CP0001` — `Utils.Mathematics.ReplacementsListType`
- `CP0001` — `Utils.Mathematics.ReplacementType`
- `CP0001` — `Utils.Mathematics.ReplacementScope`
- `CP0001` — `Utils.Mathematics.FractionType`
- `CP0001` — `Utils.Mathematics.FractionListType`
- `CP0001` — `Utils.Mathematics.LanguageType`
- `CP0001` — `Utils.Mathematics.GroupType`
- `CP0001` — `Utils.Mathematics.GroupsListType`
- `CP0001` — `Utils.Mathematics.SuffixesType`
- `CP0001` — `Utils.Mathematics.StaticNamesType`
- `CP0001` — `Utils.Mathematics.NumberScaleType`
- `CP0001` — `Utils.Mathematics.NumberToStringConverter`
- `CP0001` — `Utils.Mathematics.NumberScale`
- `CP0002` — `Utils.Objects.Bytes Utils.Objects.BytesExtensions.Join(params byte[][])`
- `CP0002` — `Utils.Objects.Bytes Utils.Objects.BytesExtensions.Join(params Utils.Objects.Bytes[])`
- `CP0002` — `int Utils.Objects.ObjectUtils.ComputeHash<T>(System.Func<T, int>, params T[])`
- `CP0001` — `Utils.Randomization.RandomEx`
- `CP0002` — `System.Collections.Generic.IReadOnlyList<Utils.Range.Range<T>> Utils.Range.Ranges<T>.Intervals.get`
- `CP0002` — `System.Collections.Generic.IEnumerable<Utils.Range.Range<T1>> Utils.Range.Ranges<T>.InnerParse<T1>(string, string, System.Collections.Generic.IEnumerable<string>, System.Func<string, T1>)`
- `CP0002` — `bool Utils.Range.Ranges<T>.Contains(Utils.Range.Range<T>)`
- `CP0002` — `void Utils.Range.Ranges<T>.AddAll(System.Collections.Generic.IEnumerable<Utils.Range.Range<T>>)`
- `CP0002` — `void Utils.Range.Ranges<T>.Add(Utils.Range.Range<T>)`
- `CP0002` — `void Utils.Range.Ranges<T>.RemoveAll(System.Collections.Generic.IEnumerable<Utils.Range.Range<T>>)`
- `CP0002` — `void Utils.Range.Ranges<T>.Remove(Utils.Range.Range<T>)`
- `CP0002` — `bool Utils.Range.Range<T>.Contains(Utils.Range.Range<T>)`
- `CP0002` — `bool Utils.Range.Range<T>.Overlap(Utils.Range.Range<T>)`
- `CP0002` — `bool Utils.Range.Range<T>.Overlap(Utils.Range.Range<T>, Utils.Range.Range<T>)`
- `CP0002` — `System.Nullable<Utils.Range.Range<T>> Utils.Range.Range<T>.Intersect(Utils.Range.Range<T>)`
- `CP0002` — `System.Collections.Generic.IEnumerable<Utils.Range.Range<double>> Utils.Range.DoubleRanges.InnerParse(string, System.Globalization.NumberFormatInfo)`
- `CP0002` — `System.Collections.Generic.IEnumerable<Utils.Range.Range<float>> Utils.Range.SingleRanges.InnerParse(string, System.Globalization.NumberFormatInfo)`
- `CP0002` — `System.Collections.Generic.IEnumerable<Utils.Range.Range<System.DateTime>> Utils.Range.DateTimeRanges.InnerParse(string, System.Globalization.DateTimeFormatInfo)`
- `CP0002` — `Utils.Range.ReadOnlyRange<T> Utils.Range.RangeUtils.Reverse<T>(this System.Collections.Generic.IReadOnlyList<T>)`
- `CP0002` — `System.Collections.Generic.IEnumerable<System.Type> Utils.Reflection.ReflectionEx.GetTypes(this System.Reflection.Assembly, System.Func<System.Type, bool>)`
- `CP0002` — `System.Collections.Generic.IEnumerable<System.Type> Utils.Reflection.ReflectionEx.GetTypes(this System.Collections.Generic.IEnumerable<System.Reflection.Assembly>, System.Func<System.Type, bool>)`
- `CP0002` — `Utils.Resources.ExternalResource.ExternalResource(string, string, System.Globalization.CultureInfo)`
- `CP0002` — `System.Security.Cryptography.HMAC Utils.Security.Authenticator.Algorithm.get`
- `CP0002` — `byte[] Utils.Security.Authenticator.Key.get`
- `CP0002` — `Utils.Security.Authenticator.Authenticator(System.Security.Cryptography.HMAC, byte[], int, int)`
- `CP0001` — `Utils.Expressions.Builders.CStyleBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.PlusOperatorBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.OperatorBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.MemberBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.NullOrMemberBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.TypeMatchBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.AddAssignationBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.AssignationBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.PostOperationBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.TypeCastBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.BracketBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.RightParenthesisBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.CloseBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.ConditionalBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.NumberConstantBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.NullBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.TrueBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.FalseBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.SizeOfBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.TypeofBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.NewBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.UnaryOperandBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.ParenthesisBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.BlockBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.DefaultUnaryBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.IfBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.BreakBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.ContinueBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.ReturnBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.WhileBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.ForBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.ForEachBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.SwitchBuilder`
- `CP0001` — `Utils.Expressions.ExpressionBuilders.ThrowParseException`

### Other binary compatibility changes

- `CP0021` — Cannot add constraint 'notnull' on type parameter 'T1' of 'Utils.Collections.DoubleIndexedDictionary<T1, T2>'.
- `CP0021` — Cannot add constraint 'notnull' on type parameter 'T2' of 'Utils.Collections.DoubleIndexedDictionary<T1, T2>'.
- `CP0021` — Cannot add constraint 'notnull' on type parameter 'K' of 'Utils.Collections.IndexedList<K, V>'.
- `CP0021` — Cannot add constraint 'notnull' on type parameter 'K' of 'Utils.Collections.LRUCache<K, V>'.
- `CP0008` — Type 'Utils.Expressions.ParserOptions' does not implement interface 'Utils.Expressions.IParserOptions' on {candidateAssembly} but it does on {baselineAssembly}
- `CP0006` — Cannot add interface member 'bool Utils.Mathematics.IAngleCalculator<T>.AreEqual(T, T, T)' to {candidateAssembly} because it does not exist on {baselineAssembly}
- `CP0006` — Cannot add interface member 'bool Utils.Mathematics.IAngleCalculator<T>.AreEqualRounded(T, T, int)' to {candidateAssembly} because it does not exist on {baselineAssembly}
- `CP0006` — Cannot add interface member 'T Utils.Mathematics.IAngleCalculator<T>.NormalizeRounded(T, int)' to {candidateAssembly} because it does not exist on {baselineAssembly}
- `CP0008` — Type 'Utils.Mathematics.FloatingPointComparer<T>' does not implement interface 'System.Collections.Generic.IEqualityComparer<T>' on {candidateAssembly} but it does on {baselineAssembly}
- `CP0009` — Type 'Utils.Security.Authenticator' has the sealed modifier on {candidateAssembly} but not on {baselineAssembly}

<a id="omy-utils-io"></a>
## omy.Utils.IO

- Published baseline: `1.2.1`
- Accepted diagnostics: **7**
- Diagnostic classes: `CP0002`: 7

### Removed or incompatible published surface

- `CP0002` — `Utils.IO.StreamCopier.StreamCopier(bool, System.Collections.Generic.IEnumerable<System.IO.Stream>)`
- `CP0002` — `Utils.IO.StreamCopier.StreamCopier(System.Collections.Generic.IEnumerable<System.IO.Stream>)`
- `CP0002` — `byte[] Utils.IO.StreamUtils.ReadToEnd(this System.IO.Stream)`
- `CP0002` — `Utils.IO.StreamValidator.StreamValidator(System.IO.Stream)`
- `CP0002` — `Utils.IO.BaseEncoding.BaseDecoderStream.BaseDecoderStream(System.IO.Stream, Utils.IO.BaseEncoding.IBaseDescriptor)`
- `CP0002` — `T[] Utils.IO.Serialization.ReaderWriterExtensions.ReadArray<T>(this Utils.IO.Serialization.Reader, int, bool)`
- `CP0002` — `void Utils.IO.Serialization.ReaderWriterExtensions.WriteVariableLengthString(this Utils.IO.Serialization.Writer, string, System.Text.Encoding, bool, int)`

<a id="omy-utils-xml"></a>
## omy.Utils.XML

- Published baseline: `1.2.1`
- Accepted diagnostics: **1**
- Diagnostic classes: `CP0002`: 1

### Removed or incompatible published surface

- `CP0002` — `System.Collections.Generic.IEnumerable<System.Xml.XmlReader> Utils.XML.XmlUtils.ReadChildElements(this System.Xml.XmlReader)`

<a id="omy-utils-net"></a>
## omy.Utils.Net

- Published baseline: `1.2.1`
- Accepted diagnostics: **19**
- Diagnostic classes: `CP0002`: 19

### Removed or incompatible published surface

- `CP0002` — `System.Threading.Tasks.Task<string> Utils.Net.EchoClient.EchoAsync(string, int, string)`
- `CP0002` — `System.Threading.Tasks.Task<string> Utils.Net.EchoClient.EchoAsync(string, string)`
- `CP0002` — `System.Threading.Tasks.Task<int> Utils.Net.IcmpUtils.SendEchoRequestAsync(System.Net.IPAddress, int, int)`
- `CP0002` — `System.Threading.Tasks.Task<int> Utils.Net.IcmpUtils.SendEchoRequestAsync(System.Net.IPAddress, Utils.Net.Icmp.IcmpPacket, int)`
- `CP0002` — `System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<Utils.Net.Icmp.TracerouteHop>> Utils.Net.IcmpUtils.TracerouteAsync(System.Net.IPAddress, int, int)`
- `CP0002` — `Utils.Net.NntpServer.NntpServer(Utils.Net.INntpArticleStore)`
- `CP0002` — `System.Threading.Tasks.Task<System.DateTime> Utils.Net.NtpClient.GetTimeAsync(string, int)`
- `CP0002` — `System.Threading.Tasks.Task<System.DateTime> Utils.Net.NtpClient.GetTimeAsync(string)`
- `CP0002` — `System.Threading.Tasks.Task<string> Utils.Net.QuoteOfTheDayClient.GetQuoteAsync(string, int)`
- `CP0002` — `System.Threading.Tasks.Task<string> Utils.Net.QuoteOfTheDayClient.GetQuoteAsync(string)`
- `CP0002` — `System.Threading.Tasks.Task Utils.Net.SmtpServer.StartAsync(System.IO.Stream, bool, System.Threading.CancellationToken)`
- `CP0002` — `System.Threading.Tasks.Task<System.DateTime> Utils.Net.TimeProtocolClient.GetTimeAsync(string, int)`
- `CP0002` — `System.Threading.Tasks.Task<System.DateTime> Utils.Net.TimeProtocolClient.GetTimeAsync(string)`
- `CP0002` — `System.Threading.Tasks.Task Utils.Net.WakeOnLan.SendMagicPacketAsync(System.Net.NetworkInformation.PhysicalAddress, System.Net.IPAddress?, int)`
- `CP0002` — `void Utils.Net.Arp.ArpPacket.HardwareType.set` — removed setter; property is now read-only (always returns 1, Ethernet)
- `CP0002` — `void Utils.Net.Arp.ArpPacket.ProtocolType.set` — removed setter; property is now read-only (always returns 0x0800, IPv4)
- `CP0002` — `void Utils.Net.Arp.ArpPacket.HardwareAddressLength.set` — removed setter; property is now read-only (always returns 6)
- `CP0002` — `void Utils.Net.Arp.ArpPacket.ProtocolAddressLength.set` — removed setter; property is now read-only (always returns 4)
- `CP0002` — `void Utils.Net.DNS.DNSHeader.Append(Utils.Net.DNS.DNSHeader)`

<a id="omy-utils-data"></a>
## omy.Utils.Data

- Published baseline: `1.2.1`
- Accepted diagnostics: **3**
- Diagnostic classes: `CP0002`: 3

### Removed or incompatible published surface

- `CP0002` — `System.Data.IDbDataParameter Utils.Data.DbCommandExtensions.AddNewParameter(this System.Data.IDbCommand, string, System.Data.DbType, object)`
- `CP0002` — `System.Data.IDbDataParameter Utils.Data.DbCommandExtensions.AddNewParameter(this System.Data.IDbCommand, string, object)`
- `CP0002` — `Utils.Data.SqlBuilderInterpolator.SqlBuilderInterpolator(int, int, System.Data.IDbConnection)`

<a id="omy-utils-fonts"></a>
## omy.Utils.Fonts

- Published baseline: `1.2.1`
- Accepted diagnostics: **22**
- Diagnostic classes: `CP0001`: 2, `CP0002`: 13, `CP0005`: 2, `CP0006`: 5

### Removed or incompatible published surface

- `CP0002` — `Utils.Fonts.TTF.TrueTypeGlyph.TrueTypeGlyph(Utils.Fonts.TTF.Tables.Glyf.GlyphBase)`
- `CP0002` — `short Utils.Fonts.TTF.Tables.AcntTable.Version.get`
- `CP0002` — `void Utils.Fonts.TTF.Tables.AcntTable.DescriptionOffset.set`
- `CP0002` — `void Utils.Fonts.TTF.Tables.AcntTable.ExtensionOffset.set`
- `CP0002` — `void Utils.Fonts.TTF.Tables.AcntTable.SecondaryOffset.set`
- `CP0002` — `Utils.Fonts.TTF.Tables.Glyf.GlyphBase[] Utils.Fonts.TTF.Tables.AcntTable.Glyphs.get`
- `CP0002` — `void Utils.Fonts.TTF.Tables.AcntTable.Glyphs.set`
- `CP0002` — `object[] Utils.Fonts.TTF.Tables.AcntTable.Extension.get`
- `CP0002` — `void Utils.Fonts.TTF.Tables.AcntTable.Extension.set`
- `CP0002` — `object[] Utils.Fonts.TTF.Tables.AcntTable.Accent.get`
- `CP0002` — `void Utils.Fonts.TTF.Tables.AcntTable.Accent.set`
- `CP0002` — `float Utils.Fonts.TTF.Tables.KernTable.GetSpacingCorrection(char, char)`
- `CP0001` — `Utils.Fonts.TTF.Tables.Acnt.AcntFormat0`
- `CP0001` — `Utils.Fonts.TTF.Tables.Acnt.AcntFormatBase`
- `CP0002` — `short Utils.Fonts.TTF.Tables.CMap.CMapFormatBase.Length.get`

### Other binary compatibility changes

- `CP0006` — Cannot add interface member 'float Utils.Fonts.IFont.Scale' to {candidateAssembly} because it does not exist on {baselineAssembly}
- `CP0006` — Cannot add interface member 'float Utils.Fonts.IFont.BaseLineY' to {candidateAssembly} because it does not exist on {baselineAssembly}
- `CP0006` — Cannot add interface member 'void Utils.Fonts.IGraphicConverter.ClosePath()' to {candidateAssembly} because it does not exist on {baselineAssembly}
- `CP0006` — Cannot add interface member 'void Utils.Fonts.IGraphicConverter.BeginDrawGlyph(float, float, System.Numerics.Matrix3x2)' to {candidateAssembly} because it does not exist on {baselineAssembly}
- `CP0006` — Cannot add interface member 'void Utils.Fonts.IGraphicConverter.EndDrawGlyph()' to {candidateAssembly} because it does not exist on {baselineAssembly}
- `CP0005` — Cannot add abstract member 'int Utils.Fonts.TTF.Tables.CMap.CMapFormatBase.Length.get' to {candidateAssembly} because it does not exist on {baselineAssembly}
- `CP0005` — Cannot add abstract member 'int Utils.Fonts.TTF.Tables.CMap.CMapFormatBase.Length' to {candidateAssembly} because it does not exist on {baselineAssembly}

### Second audit pass (TODO-2026-07-19-pass2.md, items 21-39) -- pending ApiCompat regeneration

The changes below were made after the diagnostic counts above were captured against the `1.2.1`
baseline. They are additional, deliberate 2.0 breaks on top of the inventory already recorded in
`eng/api-breaking-changes/2.0.0.json`; that manifest's Fonts section must be regenerated by
re-running the ApiCompat tool against the `1.2.1` baseline before the 2.0.0 release gate is
re-validated (hand-authoring the exact diagnostic message text here would risk not matching the
tool's actual output, which the gate compares byte-for-byte).

- `GlyphCompound.getGlyphIndex(int)` renamed to `GetGlyphIndex(int)`, returning `ushort` instead of `short`.
- `GlyphCompound.Instructions` is `ReadOnlyMemory<byte>` instead of `byte[]`.
- `CmapTable.CMaps` is `IReadOnlyList<CMapFormatBase>` instead of `CMapFormatBase[]`.
- `CmapTable.AddCMap`/`RemoveCMap`/`GetCMap` take `ushort` platform/encoding IDs instead of `short`.
- `CmapTable.Version`/`NumberSubtables` are `ushort` instead of `short`.
- `TrueTypeFont.TablesCount`/`SearchRange`/`EntrySelector`/`RangeShift` are `ushort` instead of `short`.
- `GlyphBase.Length` (and the `GlyphSimple`/`GlyphCompound` overrides) is `int` instead of `short`.
- `TrueTypeFont.TableDeclaration` removed (replaced internally by `Parsing.TableDirectoryEntry`).
- `TrueTypeFont.ParseFont(byte[])`/`ParseFont(Stream)` gained an optional `TrueTypeFontParsingOptions`
  parameter (source-compatible; recompilation required for binary compatibility).
- `TrueTypeFont.WriteFont()` gained an optional `TrueTypeFontWritingOptions` parameter (same note).
- New public types: `TrueTypeFontParsingOptions`, `TrueTypeFontWritingOptions`, `FontValidationMode`,
  `FontDiagnostic`, `FontDiagnosticCode`, `FontDiagnosticSeverity`, `FontParseException`.
- New public members: `TrueTypeFont.Diagnostics`, `TrueTypeFont.ParseFontAsync`,
  `TrueTypeFont.WriteFont(Stream, ...)`, `TrueTypeFont.WriteFontAsync`, `GlyfTable.TryGetGlyph`.
- Fonts with structural anomalies that previously parsed silently (duplicate table tags, checksum
  mismatches, malformed `cmap` subtables, out-of-range composite glyph references) now throw
  `FontParseException` by default (`FontValidationMode.Strict`); pass `FontValidationMode.Permissive`
  to restore a best-effort parse with diagnostics instead of an exception.

<a id="omy-utils-imaging"></a>
## omy.Utils.Imaging

- Published baseline: `1.2.1`
- Accepted diagnostics: **7**
- Diagnostic classes: `CP0002`: 6, `CP0006`: 1

### Removed or incompatible published surface

- `CP0002` — `Utils.Imaging.ColorAhsv Utils.Imaging.ColorAhsv.FromColorAshv<TColorAshv, T>(TColorAshv)`
- `CP0002` — `Utils.Imaging.IColorArgb<double> Utils.Imaging.ColorArgb.Substract(Utils.Imaging.IColorArgb<double>)`
- `CP0002` — `Utils.Imaging.IColorArgb<byte> Utils.Imaging.ColorArgb32.Substract(Utils.Imaging.IColorArgb<byte>)`
- `CP0002` — `Utils.Imaging.ColorArgb32 Utils.Imaging.ColorArgb32.LinearGrandient(Utils.Imaging.ColorArgb32, Utils.Imaging.ColorArgb32, float)`
- `CP0002` — `Utils.Imaging.IColorArgb<ushort> Utils.Imaging.ColorArgb64.Substract(Utils.Imaging.IColorArgb<ushort>)`
- `CP0002` — `Utils.Imaging.IColorArgb<T> Utils.Imaging.IColorArgb<T>.Substract(Utils.Imaging.IColorArgb<T>)`

### Other binary compatibility changes

- `CP0006` — Cannot add interface member 'Utils.Imaging.IColorArgb<T> Utils.Imaging.IColorArgb<T>.Subtract(Utils.Imaging.IColorArgb<T>)' to {candidateAssembly} because it does not exist on {baselineAssembly}

<a id="omy-utils-geography"></a>
## omy.Utils.Geography

- Published baseline: `1.2.1`
- Accepted diagnostics: **23**
- Diagnostic classes: `CP0002`: 16, `CP0007`: 4, `CP0008`: 1, `CP0009`: 1, `CP0012`: 1

### Removed or incompatible published surface

- `CP0002` — `int Utils.Geography.Display.RepresentationConverter<T>.GetMapSize(byte)`
- `CP0002` — `T Utils.Geography.Model.GeoPoint<T>.MinutesInDegree`
- `CP0002` — `T Utils.Geography.Model.GeoPoint<T>.SecondsInDegree`
- `CP0002` — `T Utils.Geography.Model.GeoPoint<T>.SecondsInMinute`
- `CP0002` — `Utils.Mathematics.IAngleCalculator<T> Utils.Geography.Model.GeoPoint<T>.degree`
- `CP0002` — `Utils.Mathematics.FloatingPointComparer<T> Utils.Geography.Model.GeoPoint<T>.comparer`
- `CP0002` — `System.Collections.Generic.IReadOnlyList<string> Utils.Geography.Model.GeoPoint<T>.PositiveLatitude`
- `CP0002` — `System.Collections.Generic.IReadOnlyList<string> Utils.Geography.Model.GeoPoint<T>.NegativeLatitude`
- `CP0002` — `System.Collections.Generic.IReadOnlyList<string> Utils.Geography.Model.GeoPoint<T>.PositiveLongitude`
- `CP0002` — `System.Collections.Generic.IReadOnlyList<string> Utils.Geography.Model.GeoPoint<T>.NegativeLongitude`
- `CP0002` — `Utils.Geography.Model.GeoPoint<T>.GeoPoint()`
- `CP0002` — `bool Utils.Geography.Model.GeoPoint<T>.ParseCoordinates(string, string, System.Globalization.CultureInfo, System.Text.RegularExpressions.Regex, out T, out T)`
- `CP0002` — `T Utils.Geography.Model.GeoPoint<T>.ParseCoordinate(Utils.Geography.Model.CoordinateDirection, string, System.Collections.Generic.IReadOnlyList<string>, System.Collections.Generic.IReadOnlyList<string>, System.Globalization.CultureInfo, System.Text.RegularExpressions.Regex)`
- `CP0002` — `System.Text.RegularExpressions.Regex Utils.Geography.Model.GeoPoint<T>.BuildRegexCoordinates(System.Globalization.CultureInfo)`
- `CP0002` — `void Utils.Geography.Model.MapPosition<T>.GeoPoint.set`
- `CP0002` — `void Utils.Geography.Model.MapPosition<T>.ZoomLevel.set`

### Other binary compatibility changes

- `CP0007` — Type 'Utils.Geography.Model.GeoPoint<T>' does not inherit from base type 'System.Object' on {candidateAssembly} but it does on {baselineAssembly}
- `CP0009` — Type 'Utils.Geography.Model.GeoPoint<T>' has the sealed modifier on {candidateAssembly} but not on {baselineAssembly}
- `CP0012` — Cannot remove 'virtual' keyword from member 'Utils.Geography.Model.GeoPoint<T>.ToString(string, System.IFormatProvider)'.
- `CP0007` — Type 'Utils.Geography.Model.GeoPointList<T>' does not inherit from base type 'System.Collections.Generic.List<Utils.Geography.Model.GeoPoint<T>>' on {candidateAssembly} but it does on {baselineAssembly}
- `CP0007` — Type 'Utils.Geography.Model.GeoPointList2<T>' does not inherit from base type 'System.Collections.Generic.List<Utils.Geography.Model.GeoPointList<T>>' on {candidateAssembly} but it does on {baselineAssembly}
- `CP0007` — Type 'Utils.Geography.Model.GeoVector<T>' does not inherit from base type 'Utils.Geography.Model.GeoPoint<T>' on {candidateAssembly} but it does on {baselineAssembly}
- `CP0008` — Type 'Utils.Geography.Model.GeoVector<T>' does not implement interface 'System.IEquatable<Utils.Geography.Model.GeoPoint<T>>' on {candidateAssembly} but it does on {baselineAssembly}

<a id="omy-utils-reflection"></a>
## omy.Utils.Reflection

- Published baseline: `1.2.1`
- Accepted diagnostics: **3**
- Diagnostic classes: `CP0002`: 3

### Removed or incompatible published surface

- `CP0002` — `I Utils.Reflection.LibraryMapper.Emit<I>(string, System.Runtime.InteropServices.CallingConvention)`
- `CP0002` — `T Utils.Reflection.Reflection.Emit.EmitDllMappableClass.Emit<T>(System.Runtime.InteropServices.CallingConvention)`
- `CP0002` — `object Utils.Reflection.Reflection.Emit.EmitDllMappableClass.Emit(System.Type)`

<a id="omy-utils-mathematics"></a>
## omy.Utils.Mathematics

- Published baseline: `1.2.1`
- Accepted diagnostics: **19**
- Diagnostic classes: `CP0001`: 2, `CP0002`: 16, `CP0021`: 1

### Removed or incompatible published surface

- `CP0002` — `Utils.Mathematics.Expressions.ExpressionDerivation.ExpressionDerivation(string)`
- `CP0002` — `System.Linq.Expressions.Expression Utils.Mathematics.Expressions.ExpressionIntegration.Parameter(System.Linq.Expressions.ParameterExpression, object)`
- `CP0002` — `System.Linq.Expressions.Expression Utils.Mathematics.Expressions.ExpressionIntegration.Substract(System.Linq.Expressions.BinaryExpression, System.Linq.Expressions.Expression, System.Linq.Expressions.Expression)`
- `CP0001` — `Utils.Mathematics.Fourrier.FastFourrierTransform`
- `CP0001` — `Utils.Mathematics.Fourrier.FourrierExtensions`
- `CP0002` — `(Utils.Mathematics.LinearAlgebra.Matrix<T>, Utils.Mathematics.LinearAlgebra.Matrix<T>) Utils.Mathematics.LinearAlgebra.Matrix<T>.DiagonalizeLU()`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Matrix<T> Utils.Mathematics.LinearAlgebra.Matrix<T>.Invert()`
- `CP0002` — `bool Utils.Mathematics.LinearAlgebra.Matrix<T>.IsTriangularised.get`
- `CP0002` — `bool Utils.Mathematics.LinearAlgebra.Matrix<T>.IsDiagonalized.get`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Matrix<T> Utils.Mathematics.LinearAlgebra.MatrixTransformations.Diagonal<T>(params T[])`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Matrix<T> Utils.Mathematics.LinearAlgebra.MatrixTransformations.Scaling<T>(params T[])`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Matrix<T> Utils.Mathematics.LinearAlgebra.MatrixTransformations.Skew<T>(params T[])`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Matrix<T> Utils.Mathematics.LinearAlgebra.MatrixTransformations.Rotation<T>(params T[])`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Matrix<T> Utils.Mathematics.LinearAlgebra.MatrixTransformations.Translation<T>(params T[])`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Matrix<T> Utils.Mathematics.LinearAlgebra.MatrixTransformations.Transform<T>(params T[])`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Vector<T>.Vector(params T[])`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Vector<T> Utils.Mathematics.LinearAlgebra.Vector<T>.Normalize()`
- `CP0002` — `Utils.Mathematics.LinearAlgebra.Vector<T> Utils.Mathematics.LinearAlgebra.Vector<T>.FromNormalSpace()`

### Other binary compatibility changes

- `CP0021` — Cannot remove constraint '!:System.Numerics.ITrigonometricFunctions{`0}' on type parameter 'T' of 'Utils.Mathematics.LinearAlgebra.Line<T>'.

<a id="omy-utils-odata"></a>
## omy.Utils.OData

- Published baseline: `0.0.1`
- Accepted diagnostics: **10**
- Diagnostic classes: `CP0002`: 7, `CP0009`: 1, `CP0012`: 2

### Removed or incompatible published surface

- `CP0002` — `string? Utils.OData.ODataQueryBuilder.Authorization.get`
- `CP0002` — `System.Type Utils.OData.ErrorReturnValue.EqualityContract.get`
- `CP0002` — `void Utils.OData.ErrorReturnValue.code.init`
- `CP0002` — `void Utils.OData.ErrorReturnValue.message.init`
- `CP0002` — `bool Utils.OData.ErrorReturnValue.PrintMembers(System.Text.StringBuilder)`
- `CP0002` — `Utils.OData.ErrorReturnValue.ErrorReturnValue(Utils.OData.ErrorReturnValue)`
- `CP0002` — `void Utils.OData.ErrorReturnValue.Deconstruct(out int, out string)`

### Other binary compatibility changes

- `CP0009` — Type 'Utils.OData.ErrorReturnValue' has the sealed modifier on {candidateAssembly} but not on {baselineAssembly}
- `CP0012` — Cannot remove 'virtual' keyword from member 'Utils.OData.ErrorReturnValue.Equals(Utils.OData.ErrorReturnValue?)'.
- `CP0012` — Cannot remove 'virtual' keyword from member 'Utils.OData.ErrorReturnValue.<Clone>$()'.

<a id="omy-utils-virtualmachine"></a>
## omy.Utils.VirtualMachine

- Published baseline: `0.1.0`
- Accepted diagnostics: **7**
- Diagnostic classes: `CP0002`: 6, `CP0006`: 1

### Removed or incompatible published surface

- `CP0002` — `void Utils.VirtualMachine.VirtualProcessor<T>.Execute(T)`
- `CP0002` — `byte[] Utils.VirtualMachine.Context.Data.get`
- `CP0002` — `Utils.VirtualMachine.Context.Context(byte[])`
- `CP0002` — `Utils.VirtualMachine.DefaultContext.DefaultContext(byte[])`
- `CP0002` — `System.Collections.Generic.Stack<object> Utils.VirtualMachine.DefaultContext.Stack.get`
- `CP0002` — `Utils.VirtualMachine.VirtualProcessorException.VirtualProcessorException(System.Runtime.Serialization.SerializationInfo, System.Runtime.Serialization.StreamingContext)`

### Other binary compatibility changes

- `CP0006` — Cannot add interface member 'sbyte Utils.VirtualMachine.INumberReader.ReadSByte(Utils.VirtualMachine.Context)' to {candidateAssembly} because it does not exist on {baselineAssembly}

<a id="omy-utils-dependencyinjection"></a>
## omy.Utils.DependencyInjection

- Published baseline: `1.2.1`
- Accepted diagnostics: **0**
- Diagnostic classes: none (binary compatible)

<a id="omy-utils-odata-generators"></a>
## omy.Utils.OData.Generators

- Published baseline: `0.0.1`
- Accepted diagnostics: **3**
- Diagnostic classes: `CP0002`: 2, `CP0008`: 1

### Removed or incompatible published surface

- `CP0002` — `void Utils.OData.Generators.ODataEntityGenerator.Initialize(Microsoft.CodeAnalysis.GeneratorInitializationContext)`
- `CP0002` — `void Utils.OData.Generators.ODataEntityGenerator.Execute(Microsoft.CodeAnalysis.GeneratorExecutionContext)`

### Other binary compatibility changes

- `CP0008` — Type 'Utils.OData.Generators.ODataEntityGenerator' does not implement interface 'Microsoft.CodeAnalysis.ISourceGenerator' on {candidateAssembly} but it does on {baselineAssembly}

<a id="omy-utils-io-serialization-generators"></a>
## omy.Utils.IO.Serialization.Generators

- Published baseline: `1.2.1`
- Accepted diagnostics: **3**
- Diagnostic classes: `CP0002`: 2, `CP0008`: 1

### Removed or incompatible published surface

- `CP0002` — `void Utils.IO.Serialization.Generators.ReaderWriterGenerator.Initialize(Microsoft.CodeAnalysis.GeneratorInitializationContext)`
- `CP0002` — `void Utils.IO.Serialization.Generators.ReaderWriterGenerator.Execute(Microsoft.CodeAnalysis.GeneratorExecutionContext)`

### Other binary compatibility changes

- `CP0008` — Type 'Utils.IO.Serialization.Generators.ReaderWriterGenerator' does not implement interface 'Microsoft.CodeAnalysis.ISourceGenerator' on {candidateAssembly} but it does on {baselineAssembly}

<a id="omy-utils-dependencyinjection-generators"></a>
## omy.Utils.DependencyInjection.Generators

- Published baseline: `1.2.1`
- Accepted diagnostics: **3**
- Diagnostic classes: `CP0002`: 2, `CP0008`: 1

### Removed or incompatible published surface

- `CP0002` — `void Utils.DependencyInjection.Generators.StaticAutoGenerator.Initialize(Microsoft.CodeAnalysis.GeneratorInitializationContext)`
- `CP0002` — `void Utils.DependencyInjection.Generators.StaticAutoGenerator.Execute(Microsoft.CodeAnalysis.GeneratorExecutionContext)`

### Other binary compatibility changes

- `CP0008` — Type 'Utils.DependencyInjection.Generators.StaticAutoGenerator' does not implement interface 'Microsoft.CodeAnalysis.ISourceGenerator' on {candidateAssembly} but it does on {baselineAssembly}

