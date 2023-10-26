using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Utils.Net.Expressions;
using Utils.Reflection;

namespace Utils.Net.DNS
{
    public class DNSPacketWriter : IDNSWriter<byte[]>
    {
        private readonly Action<Datas, DNSHeader> WriteHeader;
        private readonly Action<Datas, DNSRequestRecord> WriteRequestRecord;
        private readonly Action<Datas, DNSResponseRecord> WriteResponseRecord;
        private readonly Dictionary<Type, Action<Datas, DNSResponseDetail>> writers = new();
        private readonly Dictionary<string, ushort> requestClassTypes = new() { { "ALL", 0xFF } };

        public static DNSPacketWriter Default { get; } = new DNSPacketWriter(DNSFactory.Default);
        public DNSPacketWriter(params DNSFactory[] factories) : this((IEnumerable<DNSFactory>)factories) { }

        public DNSPacketWriter(IEnumerable<DNSFactory> factories)
        {
            WriteHeader = CreateReader<DNSHeader>(typeof(DNSHeader));
            WriteRequestRecord = CreateReader<DNSRequestRecord>(typeof(DNSRequestRecord));
            WriteResponseRecord = CreateReader<DNSResponseRecord>(typeof(DNSResponseRecord));

            foreach (var dnsElementType in factories.SelectMany(f => f.DNSTypes))
            {
                CreateReader(dnsElementType);
            }
        }

        private void CreateReader(Type dnsElementType)
        {
            var dnsClasses = dnsElementType.GetCustomAttributes<DNSRecordAttribute>();
            if (!dnsClasses.Any()) { throw new ArgumentException($"{dnsElementType.FullName} is not a DNS element", nameof(dnsElementType)); }
            var writer = CreateReader<DNSResponseDetail>(dnsElementType);
            writers.Add(dnsElementType, writer);
            foreach (var dnsClass in dnsClasses)
            {
                requestClassTypes.Add(dnsClass.Name ?? dnsElementType.Name, dnsClass.RecordId);
            }
        }

        private Action<Datas, T> CreateReader<T>(Type dnsElementType) where T : DNSElement
        {
            Debug.WriteLine(dnsElementType.FullName);
            var datasParameter = Expression.Parameter(typeof(Datas), "datas");
            var elementParameter = Expression.Parameter(typeof(T), "dnsElement");

            var elementVariable = Expression.Variable(dnsElementType, "element");
            Expression element;

            List<ParameterExpression> variables = new();
            var fieldsReaders = new List<Expression>();
            int insertIndex = 0;
            if (typeof(T) == dnsElementType)
            {
                element = elementParameter;
            }
            else
            {
                element = elementVariable;
                variables.Add(elementVariable);
                fieldsReaders.Add(Expression.Assign(elementVariable, Expression.Convert(elementParameter, dnsElementType)));
                insertIndex = 2;
            }

            foreach (var field in DNSPacketHelpers.GetDNSFields(dnsElementType))
            {
                if (field.Attribute.Length is string fieldName)
                {
                    var memberType = ReflectionEx.GetTypeOf(field.Member);
                    var memberTarget = ExpressionEx.CreateMemberExpression(elementVariable, fieldName, BindingFlags.Public | BindingFlags.NonPublic);
                    if (memberType == typeof(string)) {
                        fieldsReaders.Insert(
                            insertIndex ++,
                            Expression.Assign(
                                memberTarget,
                                Expression.Convert(
                                    ExpressionEx.CreateMemberExpression(
                                        ExpressionEx.CreateStaticExpression(typeof(Encoding), nameof(Encoding.UTF8)), nameof(Encoding.UTF8.GetByteCount),
                                        ExpressionEx.CreateMemberExpression(elementVariable, field.Member)
                                    ),
                                    memberTarget.Type
                                )
                            )
                        );
                    }
                    else if(memberType == typeof(byte[])) {
                        fieldsReaders.Insert(
                            insertIndex++,
                            Expression.Assign(
                                memberTarget,
                                Expression.Convert(
                                    Expression.ArrayLength(ExpressionEx.CreateMemberExpression(elementVariable, field.Member)),
                                    memberTarget.Type
                                )
                            )
                        );
                    }
                }
                Expression[] callExpression = CreateReadExpression(datasParameter, element, field.Member, field.Attribute);
                fieldsReaders.AddRange(callExpression);
            }

            var expression = Expression.Lambda<Action<Datas, T>>(
                Expression.Block(
                    variables,
                    [.. fieldsReaders]
                ),
                "Write" + dnsElementType.Name,
                new[] {
                    datasParameter,
                    elementParameter
                }
            );

            return expression.Compile();
        }

        private IReadOnlyDictionary<Type, Func<ParameterExpression, Expression, DNSFieldAttribute, Expression[]>> WriterExpressions {get;} = new Dictionary<Type, Func<ParameterExpression, Expression, DNSFieldAttribute, Expression[]>>() {
                { typeof(byte), (datasParameter, assignationSource, dnsField) => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteByte), assignationSource)] },
                { typeof(ushort), (datasParameter, assignationSource, dnsField) => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteUShort), assignationSource)] },
                { typeof(uint), (datasParameter, assignationSource, dnsField) => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteUInt), assignationSource)] },
                { typeof(DNSDomainName), (datasParameter, assignationSource, dnsField) => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteDomainName), assignationSource)] },
                {
                    typeof(byte[]),
                    (datasParameter, assignationSource, dnsField)
                        => dnsField.Length switch {
                            int length => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteBytes), assignationSource, Expression.Constant(length, typeof(int)))],
                            FieldsSizeOptions options => options switch {
                                FieldsSizeOptions.PrefixedSize1B => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteBytesPrefixed1B), assignationSource)],
                                FieldsSizeOptions.PrefixedSize2B => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteBytesPrefixed2B), assignationSource)],
                                FieldsSizeOptions.PrefixedSizeBits1B => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteBytesPrefixedBits1B), assignationSource)],
                                _ => throw new InvalidOperationException($"{options} is not a valid value")
                            },
                            _ => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteBytes), assignationSource)]
                        }
                },
                {
                    typeof(string),
                    (datasParameter, assignationSource, dnsField)
                        => dnsField.Length switch {
                            int length => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteString), assignationSource, Expression.Constant(dnsField.Length, typeof(int)))],
                            FieldsSizeOptions options => options switch {
                                FieldsSizeOptions.PrefixedSize1B => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteStringPrefixed1B), assignationSource)],
                                FieldsSizeOptions.PrefixedSize2B => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteStringPrefixed2B), assignationSource)],
                                FieldsSizeOptions.PrefixedSizeBits1B => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteStringPrefixedBits1B), assignationSource)],
                                _ => throw new InvalidOperationException($"{options} is not a valid value")
                            },
                            _ => [ExpressionEx.CreateExpressionCall(datasParameter, nameof(Datas.WriteString), assignationSource)]
                        }
                },
            };

        private Expression[] CreateReadExpression(ParameterExpression datasParameter, Expression element, MemberInfo field, DNSFieldAttribute dnsField)
        {
            Type type = ReflectionEx.GetTypeOf(field);
            Expression assignationSource = field is PropertyInfo
                ? Expression.Property(element, (PropertyInfo)field)
                : Expression.Field(element, (FieldInfo)field);

            Type underLyingType = ReflectionEx.GetUnderlyingType(type);
            if (type != underLyingType)
            {
                assignationSource = Expression.Convert(assignationSource, underLyingType);
            }

            Expression[] callExpression = null;
            if (WriterExpressions.TryGetValue(underLyingType, out var getWriterFunction))
            {
                callExpression = getWriterFunction(datasParameter, assignationSource, dnsField);
            }
            else if (
                ExpressionEx.TryGetConverter([
                    (typeof(byte[]), assignationSource),
                    (typeof(string), assignationSource)
                ], out var builderToBytes)
            )
            {
                callExpression = WriterExpressions[typeof(byte[])](datasParameter, builderToBytes, dnsField);
            }
            else
            {
                throw new NotSupportedException();
            }

            if (dnsField.Condition != null)
            {
                var conditionExpression = DNSExpression.BuildExpression(element, dnsField.Condition);
                callExpression = [Expression.IfThen(conditionExpression, callExpression.Length == 1 ? callExpression[0] : Expression.Block(callExpression))];
            }

            return callExpression;
        }

        private class Datas
        {
            public byte[] Datagram { get; init; }
            public int Position { get; set; } = 0;
            public Dictionary<string, ushort> StringsPositions { get; } = new();
            public Context Context { get; set; } = null;

            public void WriteByte(byte b)
            {
                Datagram[Position] = b;
                Position++;
                if (Context != null) { Context.Length++; }
            }

            public void WriteBytes(byte[] b) => WriteBytes(b, b.Length);

            public void WriteBytes(byte[] b, int length)
            {
                if (length == 0) length = b.Length;
                Array.Copy(b, 0, Datagram, Position, length);
                Position += length;
                if (Context != null) { Context.Length += (ushort)length; }
            }

            public void WriteBytesPrefixed1B(byte[] b)
            {
                WriteByte((byte)b.Length);
                WriteBytes(b, b.Length);
            }

            public void WriteBytesPrefixed2B(byte[] b)
            {
                WriteUShort((ushort)b.Length);
                WriteBytes(b, b.Length);
            }

            public void WriteBytesPrefixedBits1B(byte[] b)
            {
                WriteByte((byte)(b.Length * 8));
                WriteBytes(b, b.Length);
            }

            public void WriteUShort(ushort s)
            {
                WriteUShortAt(Position, s);
                Position += 2;
                if (Context != null) { Context.Length += 2; }
            }

            public void WriteUShortAt(int position, ushort s)
            {
                Datagram[position] = (byte)((s >> 8) & 0xFF);
                Datagram[position + 1] = (byte)(s & 0xFF);
            }

            public void WriteUInt(uint i)
            {
                WriteUIntAt(Position, i);
                Position += 4;
                if (Context != null) { Context.Length += 4; }
            }
            private void WriteUIntAt(int position, uint i)
            {
                Datagram[position] = (byte)((i >> 24) & 0xFF);
                Datagram[position + 1] = (byte)((i >> 16) & 0xFF);
                Datagram[position + 2] = (byte)((i >> 8) & 0xFF);
                Datagram[position + 3] = (byte)(i & 0xFF);
            }

            public void WriteString(string s, int length)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                WriteBytes(bytes, length);
            }

            public void WriteString(string s)
            {
                if (Context == null) throw new NullReferenceException("Context must not be null");
                var bytes = Encoding.UTF8.GetBytes(s);
                WriteBytes(bytes);
            }

            public void WriteStringPrefixedBits1B(string s)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                WriteBytesPrefixedBits1B(bytes);
            }

            public void WriteStringPrefixed1B(string s)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                WriteBytesPrefixed1B(bytes);
            }


            public void WriteStringPrefixed2B(string s)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                WriteBytesPrefixed2B(bytes);
            }



            public void WriteDomainName(DNSDomainName s)
            {
                if (StringsPositions.TryGetValue(s.Value, out ushort position))
                {
                    WriteUShort((ushort)(position | 0xC000));
                    return;
                }

                StringsPositions.Add(s.Value, (ushort)Position);
                WriteByte((byte)s.SubDomain.Length);
                WriteBytes(ASCIIEncoding.UTF8.GetBytes(s.SubDomain));
                if (s.ParentDomain != null)
                {
                    WriteDomainName(s.ParentDomain);
                }
                else
                {
                    WriteByte((byte)0x00);
                }

            }

        }

        private class Context
        {
            public ushort Length { get; set; }
        }


        public byte[] Write(DNSHeader header)
        {
            Datas datasStructure = new Datas()
            {
                Datagram = new byte[512],
                Position = 0
            };

            Write(datasStructure, header);
            var result = new byte[datasStructure.Position];
            Array.Copy(datasStructure.Datagram, result, datasStructure.Position);
            return result;
        }

        private void Write(Datas datas, DNSHeader header)
        {
            header.QDCount = (ushort)header.Requests.Count;
            header.ANCount = (ushort)header.Responses.Count;
            header.NSCount = (ushort)header.Authorities.Count;
            header.ARCount = (ushort)header.Additionals.Count;
            WriteHeader(datas, header);
            foreach (var requestRecord in header.Requests)
            {
                requestRecord.RequestType = requestClassTypes[requestRecord.Type];
                WriteRequestRecord(datas, requestRecord);
            }
            foreach(var responseRecord in header.Responses)
            {
                WriteResponse(datas, responseRecord);
            }
            foreach (var responseRecord in header.Authorities)
            {
                WriteResponse(datas, responseRecord);
            }
            foreach (var responseRecord in header.Additionals)
            {
                WriteResponse(datas, responseRecord);
            }
        }

        private void WriteResponse(Datas datas, DNSResponseRecord responseRecord)
        {
            WriteResponseRecord(datas, responseRecord);
            var middlePosition = datas.Position;
            datas.Context = new Context
            {
                Length = 0
            };
            writers[responseRecord.RData.GetType()](datas, responseRecord.RData);
            var endRecordPosition = datas.Position;
            responseRecord.RDLength = datas.Context.Length;
            datas.Context = null;
            datas.Position = middlePosition - 2;
            datas.WriteUShort(responseRecord.RDLength); 
            datas.Position = endRecordPosition;
        }

    }
}

