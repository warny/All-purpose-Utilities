using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Utils.Expressions;
using Utils.Reflection;

namespace Utils.Net.DNS;

/// <summary>
/// Provides helpers to translate DNS datagrams into strongly typed response structures.
/// </summary>
public class DNSPacketReader : IDNSReader<byte[]>, IDNSReader<Stream>
{
    private readonly Func<Datas, DNSHeader> ReadHeader;
    private readonly Func<Datas, DNSRequestRecord> ReadRequestRecord;
    private readonly Func<Datas, DNSResponseRecord> ReadResponseRecord;
    private readonly Dictionary<(int Class, DNSClassId ClassId), Func<Datas, DNSResponseDetail>> readers = new();
    private readonly Dictionary<int, string> requestClassNames = new() { { 0xFF, "ALL" } };

    /// <summary>
    /// Gets a packet reader configured with the default DNS record factories.
    /// </summary>
    public static DNSPacketReader Default { get; } = new DNSPacketReader(DNSFactory.Default);

    /// <summary>
    /// Initializes a new <see cref="DNSPacketReader"/> using the provided DNS factories.
    /// </summary>
    /// <param name="factories">Factories that describe how individual DNS records are materialized.</param>
    public DNSPacketReader(params DNSFactory[] factories) : this((IEnumerable<DNSFactory>)factories) { }

    /// <summary>
    /// Initializes a new <see cref="DNSPacketReader"/> using the provided DNS factories.
    /// </summary>
    /// <param name="factories">Factories that describe how individual DNS records are materialized.</param>
    public DNSPacketReader(IEnumerable<DNSFactory> factories)
    {
        ReadHeader = CreateReader<DNSHeader>(typeof(DNSHeader));
        ReadRequestRecord = CreateReader<DNSRequestRecord>(typeof(DNSRequestRecord));
        ReadResponseRecord = CreateReader<DNSResponseRecord>(typeof(DNSResponseRecord));

        foreach (var dnsElementType in factories.SelectMany(f => f.DNSTypes))
        {
            CreateReader(dnsElementType);
        }
    }

    private void CreateReader(Type dnsElementType)
    {
        var dnsClasses = dnsElementType.GetCustomAttributes<DNSRecordAttribute>().ToArray();
        if (dnsClasses.Length == 0) throw new ArgumentException($"{dnsElementType.FullName} is not a DNS element", nameof(dnsElementType));
        var reader = CreateReader<DNSResponseDetail>(dnsElementType);
        foreach (var dnsClass in dnsClasses)
        {
            readers.Add((dnsClass.RecordId, dnsClass.ClassId), reader);
            requestClassNames.Add(dnsClass.RecordId, dnsClass.Name ?? dnsElementType.Name);
        }
    }

    private Func<Datas, T> CreateReader<T>(Type dnsElementType) where T : DNSElement
    {
        Debug.WriteLine(dnsElementType.FullName);
        var datasParameter = Expression.Parameter(typeof(Datas), "datas");
        var resultVariable = Expression.Variable(dnsElementType, "result");

        var fieldsReaders = new List<Expression>();
        fieldsReaders.Add(Expression.Assign(resultVariable, Expression.New(dnsElementType)));

        foreach (var field in DNSPacketHelpers.GetDNSFields(dnsElementType))
        {
            Expression assignExpression = CreateExpression(datasParameter, resultVariable, field.Member, field.Attribute);
            fieldsReaders.Add(assignExpression);
        }

        fieldsReaders.Add(Expression.Convert(resultVariable, typeof(T))); ;

        var expression = Expression.Lambda<Func<Datas, T>>(
            Expression.Block(
                typeof(T),
                [resultVariable],
                [.. fieldsReaders]
            ),
            "Read" + dnsElementType.Name,
            [datasParameter]
        );

        return expression.Compile();
    }

    private IReadOnlyDictionary<Type, Func<ParameterExpression, ParameterExpression, DNSFieldAttribute, Expression>> ReaderExpressions { get; } =
        new Dictionary<Type, Func<ParameterExpression, ParameterExpression, DNSFieldAttribute, Expression>>()
        {
            { typeof(byte), (datasParameter, resultVariable, dnsField) => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadByte), BindingFlags.Public | BindingFlags.NonPublic) },
            { typeof(ushort), (datasParameter, resultVariable, dnsField) => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadUShort), BindingFlags.Public | BindingFlags.NonPublic) },
            { typeof(uint), (datasParameter, resultVariable, dnsField) => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadUInt), BindingFlags.Public | BindingFlags.NonPublic) },
            {
                typeof(byte[]), (datasParameter, resultVariable, dnsField)
                    => dnsField.Length switch {
                        int length => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes), BindingFlags.Public | BindingFlags.NonPublic, Expression.Constant(length, typeof(int))),
                        FieldsSizeOptions options => options switch {
                            FieldsSizeOptions.PrefixedSize1B => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes), BindingFlags.Public | BindingFlags.NonPublic, Expression.Convert(ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadByte)), typeof(int))),
                            FieldsSizeOptions.PrefixedSize2B => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes), BindingFlags.Public | BindingFlags.NonPublic, Expression.Convert(ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadUShort)), typeof(int))),
                            FieldsSizeOptions.PrefixedSizeBits1B => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes), BindingFlags.Public | BindingFlags.NonPublic, Expression.Divide (Expression.Convert(ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadByte)), typeof(int)), Expression.Constant(8))),
                            _ => throw new InvalidOperationException($"{options} is not a valid value")
                        },
                        string field => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes), BindingFlags.Public | BindingFlags.NonPublic, Expression.Convert(Expression.PropertyOrField(resultVariable, field), typeof(int))),
                        _ => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes))
                    }
            },
            {
                typeof(string), (datasParameter, resultVariable, dnsField)
                    => dnsField.Length switch {
                        int length => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadString), Expression.Constant(dnsField.Length, typeof(int))),
                        FieldsSizeOptions options => options switch {
                            FieldsSizeOptions.PrefixedSize1B => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadString), BindingFlags.Public | BindingFlags.NonPublic, Expression.Convert(ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadByte)), typeof(int))),
                            FieldsSizeOptions.PrefixedSize2B => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadString), BindingFlags.Public | BindingFlags.NonPublic, Expression.Convert(ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadUShort)), typeof(int))),
                            FieldsSizeOptions.PrefixedSizeBits1B => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadString), BindingFlags.Public | BindingFlags.NonPublic, Expression.Divide (Expression.Convert(ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadByte)), typeof(int)), Expression.Constant(8))),
                            _ => throw new InvalidOperationException($"{options} is not a valid value")
                        },
                        string field => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadString), BindingFlags.Public | BindingFlags.NonPublic, Expression.Convert(Expression.PropertyOrField(resultVariable, field), typeof(int))),
                        _ => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadString))
                    }
            },
            { typeof(DNSDomainName), (datasParameter, resultVariable, dnsField) => ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.ReadDomainName), BindingFlags.Public | BindingFlags.NonPublic) }
        }.ToImmutableDictionary();

    private Expression CreateExpression(ParameterExpression datasParameter, ParameterExpression resultVariable, MemberInfo field, DNSFieldAttribute dnsField)
    {
        Type type = (field as PropertyInfo)?.PropertyType ?? (field as FieldInfo).FieldType;
        var assignationTarget = field is PropertyInfo
            ? Expression.Property(resultVariable, (PropertyInfo)field)
            : Expression.Field(resultVariable, (FieldInfo)field);

        Type underLyingType = type.GetUnderlyingType();

        Expression callExpression = null;
        if (ReaderExpressions.TryGetValue(underLyingType, out var getFunction))
        {
            callExpression = getFunction(datasParameter, resultVariable, dnsField);
        }
        else if (
            ExpressionEx.TryGetConverter([
                (underLyingType, ReaderExpressions[typeof(byte[])](datasParameter, resultVariable, dnsField)),
                (underLyingType, ReaderExpressions[typeof(string)](datasParameter, resultVariable, dnsField))
            ], out var builderBytesRaw)
        )
        {
            callExpression = builderBytesRaw;
        }
        else
        {
            throw new NotSupportedException();
        }

        if (underLyingType != type)
        {
            callExpression = Expression.Convert(callExpression, type);
        }
        Expression assignExpression = Expression.Assign(assignationTarget, callExpression);

        if (dnsField.Condition != null)
        {
            var conditionExpression = DNSExpression.BuildExpression(resultVariable, dnsField.Condition);
            assignExpression = Expression.IfThen(conditionExpression, assignExpression);
        }

        return assignExpression;
    }

    private sealed class Datas
    {
        public byte[] Datagram { get; init; }
        public int Length { get; init; }
        public int Position { get; set; } = 0;
        public Dictionary<ushort, string> PositionsStrings { get; } = new();
        public Context Context { get; set; } = null;

        public byte ReadByte()
        {
            if (Context != null && Position >= Context.RDataEnd)
                throw new InvalidDataException($"DNS RDATA reader attempted to read beyond the declared RDLength boundary (position {Position}, RDataEnd {Context.RDataEnd}).");
            if (Position >= Datagram.Length)
                throw new InvalidDataException($"DNS datagram ended unexpectedly at position {Position}.");
            return Datagram[Position++];
        }

        public byte[] ReadBytes()
        {
            if (Context == null) throw new InvalidOperationException("ReadBytes requires an active RData context. Call BeginRData first.");
            int length = Context.RDataEnd - Position;
            return ReadBytes(length);
        }

        public byte[] ReadBytes(int length)
        {
            if (Context != null && Position + length > Context.RDataEnd)
                throw new InvalidDataException($"DNS RDATA reader attempted to read {length} bytes but only {Context.RDataEnd - Position} byte(s) remain within the declared RDLength boundary.");
            if (Position + length > Datagram.Length)
                throw new InvalidDataException($"Attempted to read {length} bytes at position {Position}, but the datagram is only {Datagram.Length} bytes.");
            byte[] result = new byte[length];
            Array.Copy(Datagram, Position, result, 0, length);
            Position += length;
            return result;
        }

        public ushort ReadUShort()
        {
            return (ushort)(
                (ReadByte() << 8)
                | ReadByte());
        }

        public uint ReadUInt()
        {
            return (uint)(
                (ReadByte() << 24)
                | (ReadByte() << 16)
                | (ReadByte() << 8)
                | (ReadByte()));
        }

        private const int MaxDomainNameDepth = 128;

        public DNSDomainName ReadDomainName() => ReadDomainName(Position, 0).Value;

        private DNSDomainName? ReadDomainName(int position, int depth)
        {
            if (depth > MaxDomainNameDepth)
                throw new InvalidDataException("DNS domain name exceeds maximum depth — possible compression pointer loop.");

            // Save both the sequential cursor (this.Position) and the RDATA context
            // before we may redirect to a compression pointer target.
            int savedPosition = this.Position;
            bool movedCursor = position != savedPosition;
            this.Position = position;
            var savedContext = Context;
            Context = null;

            try
            {
                ushort length = ReadByte();
                if (length == 0) return null;
                if ((length & 0xC0) == 0xC0)
                {
                    // Compression pointer: two high bits both set (RFC 1035 §4.1.4).
                    ushort p = (ushort)(((length & 0x3F) << 8) | ReadByte());
                    // After reading the two pointer bytes the sequential cursor must
                    // advance to just past those two bytes — not to the pointer target.
                    int afterPointer = this.Position;
                    if (PositionsStrings.TryGetValue(p, out string? cached))
                    {
                        return cached;
                    }
                    // Recurse into the pointer target with a fresh sequential cursor.
                    this.Position = p;
                    var resolved = ReadDomainName(p, depth + 1);
                    // Restore the sequential cursor to just after the two pointer bytes.
                    this.Position = afterPointer;
                    return resolved;
                }
                else if ((length & 0xC0) != 0)
                {
                    // Label type 01 or 10 — reserved, reject.
                    throw new InvalidDataException($"DNS label with reserved type bits 0x{length & 0xC0:X2} at offset {position}.");
                }
                else
                {
                    // Labels are raw octets (RFC 1035); Latin-1 maps each byte 0x00–0xFF
                    // to the same code point, preserving the raw content without throwing
                    // on byte sequences that would be invalid UTF-8.
                    DNSDomainName s = Encoding.Latin1.GetString(ReadBytes(length));
                    var next = ReadDomainName(this.Position, depth + 1);
                    s = s.Append(next);
                    // RFC 1035 §2.3.4: total name length must not exceed 255 bytes on the
                    // wire, which corresponds to 253 characters in presentation form.
                    if (s.Value.Length > 253)
                        throw new InvalidDataException($"DNS domain name exceeds the 253-character presentation limit ({s.Value.Length} chars).");
                    PositionsStrings[(ushort)position] = s;
                    return s;
                }
            }
            finally
            {
                // Always restore the RDATA context and the sequential cursor (unless
                // we were called with the same position, meaning we are the outer call).
                Context = savedContext;
                if (movedCursor) this.Position = savedPosition;
            }
        }

        public string ReadString()
        {
            if (Context == null) throw new InvalidOperationException("ReadString requires an active RData context. Call BeginRData first.");
            return ReadString(Context.RDataEnd - Position);
        }

        public string ReadString(int length) => Encoding.UTF8.GetString(ReadBytes(length));
    }

    private sealed class Context
    {
        public int Length { get; init; }
        // Absolute datagram offset one past the last RDATA byte.
        public int RDataEnd { get; init; }
    }


    /// <summary>
    /// Reads a DNS datagram represented as a byte array and returns the parsed header.
    /// </summary>
    /// <param name="datas">The datagram content to parse.</param>
    /// <returns>The populated <see cref="DNSHeader"/> representing the packet.</returns>
    // DNS header is 12 bytes (ID + Flags + 4 × 2-byte counts).
    private const int DnsHeaderLength = 12;

    public DNSHeader Read(byte[] datas)
    {
        if (datas.Length < DnsHeaderLength)
        {
            throw new InvalidDataException($"DNS datagram too short ({datas.Length} bytes); minimum is {DnsHeaderLength} bytes.");
        }
        Datas datasStructure = new Datas()
        {
            Datagram = datas,
            Length = datas.Length
        };

        return Read(datasStructure);
    }

    /// <summary>
    /// Reads a DNS datagram from the provided stream and returns the parsed header.
    /// </summary>
    /// <param name="datas">The stream that supplies the DNS payload.</param>
    /// <returns>The populated <see cref="DNSHeader"/> representing the packet.</returns>
    public DNSHeader Read(Stream datas)
    {
        var buffer = new byte[512];
        var length = datas.Read(buffer, 0, 512);

        if (length < DnsHeaderLength)
        {
            throw new InvalidDataException($"DNS datagram too short ({length} bytes); minimum is {DnsHeaderLength} bytes.");
        }

        // Trim the array to the actual received length so that ReadByte / ReadBytes
        // cannot silently advance into zero-filled uninitialized tail bytes.
        var datagram = new byte[length];
        Array.Copy(buffer, datagram, length);

        Datas datasStructure = new Datas()
        {
            Datagram = datagram,
            Length = length
        };

        return Read(datasStructure);
    }

    private DNSHeader Read(Datas datas)
    {
        var header = ReadHeader(datas);
        ReadRequestRecords(datas, header.QDCount, header.Requests);
        ReadResponseRecords(datas, header.ANCount, header.Responses);
        ReadResponseRecords(datas, header.NSCount, header.Authorities);
        ReadResponseRecords(datas, header.ARCount, header.Additionals);
        return header;
    }

    private void ReadRequestRecords(Datas datas, ushort count, IList<DNSRequestRecord> requests)
    {
        for (int i = 0; i < count; i++)
        {
            var requestRecord = ReadRequestRecord(datas);
            requestRecord.Type = requestClassNames.TryGetValue(requestRecord.RequestType, out string? typeName)
                ? typeName
                : requestRecord.RequestType.ToString();
            requests.Add(requestRecord);
        }
    }

    private void ReadResponseRecords(Datas datas, ushort count, IList<DNSResponseRecord> records)
    {
        for (int i = 0; i < count; i++)
        {
            var responseRecord = ReadResponse(datas);
            records.Add(responseRecord);
        }
    }

    private DNSResponseRecord ReadResponse(Datas datas)
    {
        var responseRecord = ReadResponseRecord(datas);
        int rdataStart = datas.Position;
        datas.Context = new Context
        {
            Length = responseRecord.RDLength,
            RDataEnd = rdataStart + responseRecord.RDLength
        };
        string recordTypeName = requestClassNames.TryGetValue(responseRecord.Class, out string? rtn) ? rtn : responseRecord.Class.ToString();
        Debug.WriteLine($"Read record {recordTypeName}. Length = {responseRecord.RDLength}");
        if (readers.TryGetValue((responseRecord.Class, responseRecord.ClassId), out var reader))
        {
            responseRecord.RData = reader(datas);
            int consumed = datas.Position - rdataStart;
            if (consumed > responseRecord.RDLength)
                throw new InvalidDataException($"DNS record reader for {recordTypeName} consumed {consumed} bytes but RDLength is only {responseRecord.RDLength}.");
            if (consumed < responseRecord.RDLength)
            {
                // Reader did not consume all declared bytes; advance to the RDATA boundary
                // to keep the parser cursor in sync with surrounding records.
                datas.Context = null;
                datas.Position = rdataStart + responseRecord.RDLength;
            }
        }
        else
        {
            // Unknown or unsupported record type: consume the RDATA bytes to keep the parser in sync,
            // leave RData as null so the caller can still process surrounding records.
            if (responseRecord.RDLength > 0)
            {
                datas.ReadBytes(responseRecord.RDLength);
            }
        }
        datas.Context = null;
        return responseRecord;
    }

}
