using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Utils.Net.DNS
{
    public class DNSPacketReader : IDNSReader<byte[]>, IDNSReader<Stream>
    {
        private readonly Func<Datas, DNSHeader> ReadHeader;
        private readonly Func<Datas, DNSRequestRecord> ReadRequestRecord;
        private readonly Func<Datas, DNSResponseRecord> ReadResponseRecord;
        private readonly Dictionary<int, Func<Datas, DNSResponseDetail>> readers = new();
        private readonly Dictionary<int, string> requestClassNames = new() { { 0xFF, "ALL" } };

        public static DNSPacketReader Default { get; } = new DNSPacketReader(DNSFactory.DNSTypes);

        public DNSPacketReader(params Type[] dnsElementTypes) {
            ReadHeader = CreateReader<DNSHeader>(typeof(DNSHeader));
            ReadRequestRecord = CreateReader<DNSRequestRecord>(typeof(DNSRequestRecord));
            ReadResponseRecord = CreateReader<DNSResponseRecord>(typeof(DNSResponseRecord));

            foreach (var dnsElementType in dnsElementTypes)
            {
                CreateReader(dnsElementType);
            }
        }

        private void CreateReader(Type dnsElementType)
        {
            var dnsClasses = dnsElementType.GetCustomAttributes<DNSClassAttribute>();
            if (!dnsClasses.Any()) { throw new ArgumentException($"{dnsElementType.FullName} is not a DNS element", nameof(dnsElementType)); }
            var reader = CreateReader<DNSResponseDetail>(dnsElementType);
            foreach (var dnsClass in dnsClasses)
            {
                readers.Add(dnsClass.ClassId, reader);
                requestClassNames.Add(dnsClass.ClassId, dnsClass.Name ?? dnsElementType.Name);
            }
        }

        private Func<Datas, T> CreateReader<T>(Type dnsElementType) where T : DNSElement
        {
            Debug.WriteLine(dnsElementType.FullName);
            var datasParameter = Expression.Parameter(typeof(Datas), "datas");
            var resultVariable = Expression.Variable(dnsElementType, "result");

            var fieldsReaders = new List<Expression>();
            fieldsReaders.Add(Expression.Assign(resultVariable, Expression.New(dnsElementType)));

            foreach (var field in dnsElementType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(m=>m is PropertyInfo || m is FieldInfo))
            {
                var dnsField = field.GetCustomAttribute<DNSFieldAttribute>();
                if (dnsField is null) continue;

                Type type = (field as PropertyInfo)?.PropertyType ?? (field as FieldInfo).FieldType;
                var assignationTarget = field is PropertyInfo
                    ? Expression.Property(resultVariable, (PropertyInfo)field)
                    : Expression.Field(resultVariable, (FieldInfo)field);

                var uType = type.IsEnum ? type.GetEnumUnderlyingType() : type;

                Expression callExpression = null;
                if (uType == typeof(byte[]) && dnsField.Length > 0)
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes), Expression.Constant(dnsField.Length, typeof(int)));
                }
                else if (uType == typeof(byte[]))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes));
                }
                else if (uType == typeof(string) && dnsField.Length > 0)
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.ReadString), Expression.Constant(dnsField.Length, typeof(int)));
                }
                else if (uType == typeof(string))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.ReadString));
                }
                else if (uType == typeof(DNSDomainName))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.ReadDomainName));
                }
                else if (uType == typeof(byte))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.ReadByte));
                }
                else if (uType == typeof(ushort))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.ReadUShort));
                }
                else if (uType == typeof(uint))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.ReadUInt));
                }
                else if (dnsField.Length == 0 && GetObjectBuilder(uType, CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes)), out var builderBytesRaw))
                {
                    callExpression = builderBytesRaw;
                }
                else if (GetObjectBuilder(uType, CreateExpressionCall(datasParameter, nameof(Datas.ReadBytes), Expression.Constant(dnsField.Length, typeof(int))), out var builderBytes))
                {
                    callExpression = builderBytes;
                }
                else if (dnsField.Length == 0 && GetObjectBuilder(uType, CreateExpressionCall(datasParameter, nameof(Datas.ReadString)), out var builderStringRaw))
                {
                    callExpression = builderStringRaw;
                }
                else if (GetObjectBuilder(uType, CreateExpressionCall(datasParameter, nameof(Datas.ReadString), Expression.Constant(dnsField.Length, typeof(int))), out var builderString))
                {
                    callExpression = builderString;
                }
                else
                {
                    throw new NotSupportedException();
                }

                if (uType != type) {
                    callExpression = Expression.Convert(callExpression, type);
                } 
                fieldsReaders.Add(Expression.Assign(assignationTarget, callExpression));


            }
            fieldsReaders.Add(Expression.Convert(resultVariable, typeof(T))); ;

            var expression = Expression.Lambda<Func<Datas, T>>(
                Expression.Block(
                    typeof(T),
                    new[] {
                        resultVariable
                    },
                    fieldsReaders.ToArray()
                ),
                "Read" + dnsElementType.Name,
                new [] {
                    datasParameter
                }
            );

            return expression.Compile();
        }

        private static bool GetObjectBuilder(Type type, Expression datasExpression, out Expression builder)
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

            public DNSDomainName ReadDomainName() => ReadDomainName(Position);

            private string ReadDomainName(int position)
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
                    var s = Encoding.UTF8.GetString(ReadBytes(length));
                    var next = ReadDomainName(this.Position);
                    if (next is not null) s+= "." + next;
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
            for (int i = 0; i < header.QDCount; i++)
            {
                var requestRecord = ReadRequestRecord(datas);
                requestRecord.Type = requestClassNames[requestRecord.RequestType];
                header.Requests.Add(requestRecord);
            }
            for (int i = 0; i < header.ANCount; i++)
            {
                var responseRecord = ReadResponse(datas);
                header.Responses.Add(responseRecord);
            }
            for (int i = 0; i < header.NSCount; i++)
            {
                var responseRecord = ReadResponse(datas);
                header.Authorities.Add(responseRecord);
            }
            for (int i = 0; i < header.ARCount; i++)
            {
                var responseRecord = ReadResponse(datas);
                header.Additionals.Add(responseRecord);
            }

            return header;
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
}
