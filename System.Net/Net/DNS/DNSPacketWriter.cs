using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace Utils.Net.DNS
{
    public class DNSPacketWriter : IDNSWriter<byte[]>
    {
        private readonly Action<Datas, DNSHeader> WriteHeader;
        private readonly Action<Datas, DNSRequestRecord> WriteRequestRecord;
        private readonly Action<Datas, DNSResponseRecord> WriteResponseRecord;
        private readonly Dictionary<Type, Action<Datas, DNSResponseDetail>> writers = new();
        private readonly Dictionary<string, ushort> requestClassTypes = new() { { "ALL", 0xFF } };

        public static DNSPacketWriter Default { get; } = new DNSPacketWriter(DNSFactory.DNSTypes);

        public DNSPacketWriter(params Type[] dnsElementTypes)
        {
            WriteHeader = CreateWriter<DNSHeader>(typeof(DNSHeader));
            WriteRequestRecord = CreateWriter<DNSRequestRecord>(typeof(DNSRequestRecord));
            WriteResponseRecord = CreateWriter<DNSResponseRecord>(typeof(DNSResponseRecord));

            foreach (var dnsElementType in dnsElementTypes)
            {
                CreateReader(dnsElementType);
            }
        }

        private void CreateReader(Type dnsElementType)
        {
            var dnsClasses = dnsElementType.GetCustomAttributes<DNSClassAttribute>();
            if (!dnsClasses.Any()) { throw new ArgumentException($"{dnsElementType.FullName} is not a DNS element", nameof(dnsElementType)); }
            var writer = CreateWriter<DNSResponseDetail>(dnsElementType);
            writers.Add(dnsElementType, writer);
            foreach (var dnsClass in dnsClasses)
            {
                requestClassTypes.Add(dnsClass.Name ?? dnsElementType.Name, dnsClass.ClassId);
            }
        }

        private Action<Datas, T> CreateWriter<T>(Type dnsElementType) where T : DNSElement
        {
            Debug.WriteLine(dnsElementType.FullName);
            var datasParameter = Expression.Parameter(typeof(Datas), "datas");
            var elementParameter = Expression.Parameter(typeof(T), "dnsElement");

            var elementVariable = Expression.Variable(dnsElementType, "element");
            Expression element;

            ParameterExpression[] variables;
            var fieldsReaders = new List<Expression>();
            if (typeof(T) == dnsElementType)
            {
                element = elementParameter;
                variables = [];
            }
            else
            {
                element = elementVariable;
                variables = [elementVariable];
                fieldsReaders.Add(Expression.Assign(elementVariable, Expression.Convert(elementParameter, dnsElementType)));
            }

            foreach (var field in dnsElementType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(m => m is PropertyInfo || m is FieldInfo))
            {
                var dnsField = field.GetCustomAttribute<DNSFieldAttribute>();
                if (dnsField is null) continue;

                Type type = (field as PropertyInfo)?.PropertyType ?? (field as FieldInfo).FieldType;
                Expression assignationSource = field is PropertyInfo
                    ? Expression.Property(element, (PropertyInfo)field)
                    : Expression.Field(element, (FieldInfo)field);

                var uType = type.IsEnum ? type.GetEnumUnderlyingType() : type;
                if (type.IsEnum)
                {
                    assignationSource = Expression.Convert(assignationSource, uType);
                }

                Expression callExpression = null;
                if (uType == typeof(string) && dnsField.Length > 0)
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteString), assignationSource, Expression.Constant(dnsField.Length, typeof(int)));
                }
                else if (uType == typeof(string))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteString), assignationSource);
                }
                else if (uType == typeof(DNSDomainName))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteDomainName), assignationSource);
                }
                else if (uType == typeof(byte[]) && dnsField.Length > 0)
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteBytes), assignationSource, Expression.Constant(dnsField.Length, typeof(int)));
                }
                else if (uType == typeof(byte[]))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteBytes), assignationSource);
                }
                else if (uType == typeof(byte))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteByte), assignationSource);
                }
                else if (uType == typeof(ushort))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteUShort), assignationSource);
                }
                else if (uType == typeof(uint))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteUInt), assignationSource);
                }
                else if (GetObjectConverter(assignationSource, typeof(byte[]), out var builderToBytes))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteBytes), builderToBytes);
                }
                else if (GetObjectConverter(assignationSource, typeof(string), out var builderToString))
                {
                    callExpression = CreateExpressionCall(datasParameter, nameof(Datas.WriteString), builderToString);
                }
                else
                {
                    throw new NotSupportedException();
                }

                fieldsReaders.Add(callExpression);
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

        private static Expression CreateExpressionCall(ParameterExpression datasParameter, string name, params Expression[] arguments)
        {
            Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
            var method = typeof(Datas).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, argumentTypes, null);
            Expression callExpression = Expression.Call(datasParameter, method, arguments);
            return callExpression;
        }

        private static bool GetObjectConverter(Expression source, Type outType, out Expression builder)
        {
            var methodStatic = source.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m =>  m.ReturnType == outType && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == source.Type).FirstOrDefault();
            if (methodStatic != null)
            {
                builder = Expression.Call(null, methodStatic, source);
                return true;
            }

            var methodInstance = source.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.ReturnType == outType && m.GetParameters().Length == 0).FirstOrDefault();
            if (methodInstance != null)
            {
                builder = Expression.Call(source, methodInstance);
                return true;
            }


            builder = null;
            return false;
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
                Array.Copy(b, 0, Datagram, Position, length);
                Position += length;
                if (Context != null) { Context.Length += (ushort)length; }
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
            foreach (var responseRecord in header.Responses)
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
