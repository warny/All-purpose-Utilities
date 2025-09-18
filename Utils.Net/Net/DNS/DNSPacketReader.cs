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
    public DNSPacketReader(params DNSFactory[] factories) : this((IEnumerable<DNSFactory>)factories) {}

    /// <summary>
    /// Initializes a new <see cref="DNSPacketReader"/> using the provided DNS factories.
    /// </summary>
    /// <param name="factories">Factories that describe how individual DNS records are materialized.</param>
    public DNSPacketReader(IEnumerable<DNSFactory> factories) {
        ReadHeader = CreateReader<DNSHeader>(typeof(DNSHeader));
        ReadRequestRecord = CreateReader<DNSRequestRecord>(typeof(DNSRequestRecord));
        ReadResponseRecord = CreateReader<DNSResponseRecord>(typeof(DNSResponseRecord));

        foreach (var dnsElementType in factories.SelectMany(f=>f.DNSTypes))
        {
            CreateReader(dnsElementType);
        }
    }

    private void CreateReader(Type dnsElementType)
    {
        var dnsClasses = dnsElementType.GetCustomAttributes<DNSRecordAttribute>();
        if (!dnsClasses.Any()) { throw new ArgumentException($"{dnsElementType.FullName} is not a DNS element", nameof(dnsElementType)); }
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

        foreach (var field in DNSPacketHelpers.GetDNSFields(dnsElementType)) {
            Expression assignExpression = CreateExpression(datasParameter, resultVariable, field.Member, field.Attribute);
            fieldsReaders.Add(assignExpression);
        }

        fieldsReaders.Add(Expression.Convert(resultVariable, typeof(T))); ;

        var expression = Expression.Lambda<Func<Datas, T>>(
            Expression.Block(
                typeof(T),
                [ resultVariable ],
                [.. fieldsReaders]
            ),
            "Read" + dnsElementType.Name,
            [ datasParameter ]
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
            if (Context != null) { Context.BytesLeft--; }
            return Datagram[Position++];
        }

        public byte[] ReadBytes()
        {
            if (Context == null) throw new NullReferenceException("Context must not be null");
            var length = Context.BytesLeft;
            return ReadBytes(length);
        }

        public byte[] ReadBytes(int length)
        {
            byte[] result = new byte[length];
            Array.Copy(Datagram, Position, result, 0, length);
            Position += length;
            if (Context != null) { Context.BytesLeft -= length; }
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

        public DNSDomainName ReadDomainName() => ReadDomainName(Position).Value;

        private DNSDomainName? ReadDomainName(int position)
        {
            bool restorePosition = position != this.Position;
            int temp = this.Position;
            this.Position = position;
            var context = Context;
            Context = null;
            ushort length = ReadByte();
            if (length == 0) return null;
            if ((length & 0xC0) != 0)
            {
                ushort p = (ushort)(((length & 0x3F) << 8) | ReadByte());
                if (PositionsStrings.TryGetValue(p, out string s))
                {
                    if (restorePosition) this.Position = temp;
                    Context = context;
                    return s;
                }
                else
                {
                    var rs = ReadDomainName(p);
                    Context = context;
                    return rs;
                }
            }
            else
            {
                DNSDomainName s = Encoding.UTF8.GetString(ReadBytes(length));
                var next = ReadDomainName(this.Position);
                s = s.Append(next);
                if (restorePosition) this.Position = temp;
                PositionsStrings[(ushort)position] = s;
                Context = context;
                return s;
            }

        }

        public string ReadString()
        {
            if (Context == null) throw new NullReferenceException("Context must not be null");
            return ReadString(Context.BytesLeft);
        }

        public string ReadString(int length) => Encoding.UTF8.GetString(ReadBytes(length));
    }

    private sealed class Context
    {
        public int Length { get; init; }
        public int BytesLeft { get; set; }
    }


    /// <summary>
    /// Reads a DNS datagram represented as a byte array and returns the parsed header.
    /// </summary>
    /// <param name="datas">The datagram content to parse.</param>
    /// <returns>The populated <see cref="DNSHeader"/> representing the packet.</returns>
    public DNSHeader Read(byte[] datas)
    {
        Datas datasStructure = new Datas() {
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
        var datagram = new byte[512];
        var length = datas.Read(datagram, 0, 512);


        Datas datasStructure = new Datas() { 
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
            requestRecord.Type = requestClassNames[requestRecord.RequestType];
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
        datas.Context = new Context
        {
             BytesLeft = responseRecord.RDLength,
             Length = responseRecord.RDLength
        };
        Debug.WriteLine($"Read record {requestClassNames[responseRecord.Class]}. Length = {responseRecord.RDLength}");
        var responseDetail = readers[(responseRecord.Class, responseRecord.ClassId)](datas);
        responseRecord.RData = responseDetail;
        datas.Context = null;
        return responseRecord;
    }

}
