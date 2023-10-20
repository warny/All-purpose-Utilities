using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Net.DNS;

public class DNSPacketReader : IDNSReader<byte[]>, IDNSReader<Stream>
{
    private readonly Func<Datas, DNSHeader> ReadHeader;
    private readonly Func<Datas, DNSRequestRecord> ReadRequestRecord;
    private readonly Func<Datas, DNSResponseRecord> ReadResponseRecord;
    private readonly Dictionary<int, Func<Datas, DNSResponseDetail>> readers = new();
    private readonly Dictionary<int, string> requestClassNames = new() { { 0xFF, "ALL" } };

    public static DNSPacketReader Default { get; } = new DNSPacketReader(DNSFactory.Default);

    public DNSPacketReader(params DNSFactory[] factories) : this((IEnumerable<DNSFactory>)factories) {}

    public DNSPacketReader(IEnumerable<DNSFactory> factories) {
        ReadHeader = CreateReader<DNSHeader>(typeof(DNSHeader));
        ReadRequestRecord = CreateReader<DNSRequestRecord>(typeof(DNSRequestRecord));
        ReadResponseRecord = CreateReader<DNSResponseRecord>(typeof(DNSResponseRecord));

        foreach (var factory in factories)
        {
            foreach (var dnsElementType in factory.DNSTypes)
            {
                CreateReader(dnsElementType);
            }
        }
    }

    private void CreateReader(Type dnsElementType)
    {
        var dnsClasses = dnsElementType.GetCustomAttributes<DNSRecordAttribute>();
        if (!dnsClasses.Any()) { throw new ArgumentException($"{dnsElementType.FullName} is not a DNS element", nameof(dnsElementType)); }
        var reader = CreateReader<DNSResponseDetail>(dnsElementType);
        foreach (var dnsClass in dnsClasses)
        {
            readers.Add(dnsClass.RecordId, reader);
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

        foreach(var field in DNSPacketHelpers.GetDNSFields(dnsElementType)) {
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

    private IReadOnlyDictionary<Type, Func<ParameterExpression, DNSFieldAttribute, Expression>> ReaderExpressions { get; } = 
        new Dictionary<Type, Func<ParameterExpression, DNSFieldAttribute, Expression>>()
        {
            { typeof(byte), (datasParameter, dnsField) => CreateExpressionCall(datasParameter, nameof(Datas.ReadByte)) },
            { typeof(ushort), (datasParameter, dnsField) => CreateExpressionCall(datasParameter, nameof(Datas.ReadUShort)) },
            { typeof(uint), (datasParameter, dnsField) => CreateExpressionCall(datasParameter, nameof(Datas.ReadUInt)) },
            {
                typeof(byte[]), (datasParameter, dnsField)
                    => dnsField.Length != 0
                    ? CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes), Expression.Constant(dnsField.Length, typeof(int)))
                    : CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes))
            },
            {
                typeof(string), (datasParameter, dnsField)
                    => dnsField.Length != 0
                    ? CreateExpressionCall(datasParameter, nameof(Datas.ReadString), Expression.Constant(dnsField.Length, typeof(int)))
                    : CreateExpressionCall(datasParameter, nameof(Datas.ReadString))
            },
            { typeof(DNSDomainName), (datasParameter, dnsField) => CreateExpressionCall(datasParameter, nameof(Datas.ReadDomainName)) }
        }.ToImmutableDictionary();

    private Expression CreateExpression(ParameterExpression datasParameter, ParameterExpression resultVariable, MemberInfo field, DNSFieldAttribute dnsField)
    {
        Type type = (field as PropertyInfo)?.PropertyType ?? (field as FieldInfo).FieldType;
        var assignationTarget = field is PropertyInfo
            ? Expression.Property(resultVariable, (PropertyInfo)field)
            : Expression.Field(resultVariable, (FieldInfo)field);

        var uType = type.IsEnum ? type.GetEnumUnderlyingType() : type;

        Expression callExpression = null;
        if (ReaderExpressions.TryGetValue(uType, out var getFunction) ) {
            callExpression = getFunction(datasParameter, dnsField);
        }
        else if (TryGetObjectBuilder(uType, ReaderExpressions[typeof(byte[])](datasParameter, dnsField), out var builderBytesRaw))
        {
            callExpression = builderBytesRaw;
        }
        else if (TryGetObjectBuilder(uType, ReaderExpressions[typeof(string)](datasParameter, dnsField), out var builderString))
        {
            callExpression = builderBytesRaw;
        }
        else
        {
            throw new NotSupportedException();
        }

        if (uType != type)
        {
            callExpression = Expression.Convert(callExpression, type);
        }
        Expression assignExpression = Expression.Assign(assignationTarget, callExpression);

        if (dnsField.Condition != null)
        {
            var conditionExpression = DNSPacketHelpers.ConditionBuilder(resultVariable, dnsField.Condition);
            assignExpression = Expression.IfThen(conditionExpression, assignExpression);
        }

        return assignExpression;
    }

    private static bool TryGetObjectBuilder(Type type, Expression datasExpression, out Expression builder)
    {
        var constructor = type.GetConstructor([datasExpression.Type]);
        if (constructor != null)
        {
            builder = Expression.New(constructor, datasExpression);
            return true;
        }
        var  method = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod).Where(m=>m.ReturnType == type && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == datasExpression.Type).FirstOrDefault();
        if (method != null)
        {
            builder = Expression.Call(method, datasExpression);
            return true;
        }
        builder = null;
        return false;
    }

    private static Expression CreateExpressionCall(ParameterExpression datasParameter, string name, params Expression[] arguments)
    {
        Type[] argumentTypes = arguments.Select(a=>a.Type).ToArray();
        var method = typeof(Datas).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, argumentTypes, null);
        Expression callExpression = Expression.Call(datasParameter, method, arguments);
        return callExpression;
    }

    private class Datas
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
            if (length == FieldConstants.PREFIXED_SIZE)
            {
                length = ReadByte();
            }
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

    private class Context
    {
        public int Length { get; init; }
        public int BytesLeft { get; set; }
    }


    public DNSHeader Read(byte[] datas)
    {
        Datas datasStructure = new Datas() {
            Datagram = datas,
            Length = datas.Length
        };

        return Read(datasStructure);
    }

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
        Debug.WriteLine($"Read record {requestClassNames[responseRecord.Type]}. Length = {responseRecord.RDLength}");
        var responseDetail = readers[responseRecord.Type](datas);
        responseRecord.RData = responseDetail;
        datas.Context = null;
        return responseRecord;
    }

}
