using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Utils.Expressions;
using Utils.Reflection;

namespace Utils.Net.DNS;

/// <summary>
/// Provides functionality to serialize DNS packets into a <see cref="byte"/> array using reflection
/// and expression trees. This class can handle standard DNS headers, request records, response records,
/// and user-defined DNS response details annotated with <see cref="DNSRecordAttribute"/>.
/// </summary>
/// <remarks>
/// The <see cref="DNSCanonicalWriter"/> scans assemblies (via <see cref="DNSFactory"/>) for <see cref="DNSResponseDetail"/>
/// types marked with <see cref="DNSRecordAttribute"/>. It then generates expression-based writers that
/// serialize each DNS element's fields into the DNS wire format. When writing a DNS packet:
/// <list type="bullet">
/// <item><description>The DNS header is written first, updating record counts (QDCount, ANCount, etc.).</description></item>
/// <item><description>Each request record is written, translating its string <c>Type</c> to a numeric code (requestClassTypes).</description></item>
/// <item><description>Each response record is written, including the user-defined RData portion.</description></item>
/// </list>
/// The final output is a <c>byte[]</c> representing the DNS packet to be sent over the network or stored.
/// </remarks>
public class DNSCanonicalWriter : IDNSWriter<byte[]>
{
    /// <summary>
    /// A compiled expression-based writer delegate for <see cref="DNSHeader"/> objects.
    /// </summary>
    private readonly Action<Datas, DNSHeader> WriteHeader;

    /// <summary>
    /// A compiled expression-based writer delegate for <see cref="DNSRequestRecord"/> objects.
    /// </summary>
    private readonly Action<Datas, DNSRequestRecord> WriteRequestRecord;

    /// <summary>
    /// A compiled expression-based writer delegate for <see cref="DNSResponseRecord"/> objects.
    /// </summary>
    private readonly Action<Datas, DNSResponseRecord> WriteResponseRecord;

    /// <summary>
    /// Maintains compiled expression-based writers for each DNS record detail type
    /// (<see cref="DNSResponseDetail"/>). Each entry is keyed by the .NET type.
    /// </summary>
    private readonly Dictionary<Type, Action<Datas, DNSResponseDetail>> writers = new();

    /// <summary>
    /// Maps record type names (e.g., "A", "AAAA", or a custom name) to their corresponding 16-bit numeric
    /// code for DNS requests (e.g., 0x01, 0x1C, etc.). Defaults to including a mapping for "ALL" → 0xFF.
    /// </summary>
    private readonly Dictionary<string, ushort> requestClassTypes = new()
    {
        { "ALL", 0xFF }
    };

    /// <summary>
    /// Gets the default <see cref="DNSCanonicalWriter"/> instance, which scans the DNS types
    /// from <see cref="DNSFactory.Default"/>.
    /// </summary>
    public static DNSCanonicalWriter Default { get; } = new DNSCanonicalWriter(DNSFactory.Default);

    /// <summary>
    /// Initializes a new instance of the <see cref="DNSCanonicalWriter"/> class, combining
    /// the given <see cref="DNSFactory"/> instances into one writer.
    /// </summary>
    /// <param name="factories">One or more <see cref="DNSFactory"/> instances to load DNS types from.</param>
    public DNSCanonicalWriter(params DNSFactory[] factories)
        : this((IEnumerable<DNSFactory>)factories)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DNSCanonicalWriter"/> class, scanning the given
    /// <see cref="DNSFactory"/> collections for DNS record detail types.
    /// </summary>
    /// <param name="factories">A collection of <see cref="DNSFactory"/> instances to load DNS types from.</param>
    public DNSCanonicalWriter(IEnumerable<DNSFactory> factories)
    {
        // Create expression-based writers for the top-level DNS element types.
        WriteHeader = CreateReader<DNSHeader>(typeof(DNSHeader));
        WriteRequestRecord = CreateReader<DNSRequestRecord>(typeof(DNSRequestRecord));
        WriteResponseRecord = CreateReader<DNSResponseRecord>(typeof(DNSResponseRecord));

        // For each discovered DNS type, compile a writer for it and record the type code mapping.
        foreach (var dnsElementType in factories.SelectMany(f => f.DNSTypes))
        {
            CreateReader(dnsElementType);
        }
    }

    /// <summary>
    /// Creates and caches an expression-based writer for a given DNS detail type.
    /// Also updates the <see cref="requestClassTypes"/> lookup with the record ID from <see cref="DNSRecordAttribute"/>.
    /// </summary>
    /// <param name="dnsElementType">A type deriving from <see cref="DNSResponseDetail"/>, annotated with <see cref="DNSRecordAttribute"/>.</param>
    /// <exception cref="ArgumentException">Thrown if the specified type is not annotated with <see cref="DNSRecordAttribute"/>.</exception>
    private void CreateReader(Type dnsElementType)
    {
        var dnsClasses = dnsElementType.GetCustomAttributes<DNSRecordAttribute>();
        if (!dnsClasses.Any())
        {
            throw new ArgumentException($"{dnsElementType.FullName} is not a DNS element", nameof(dnsElementType));
        }

        // Build an expression-based writer for the user-defined DNS detail type (e.g., A-record, AAAA-record, etc.).
        var writer = CreateReader<DNSResponseDetail>(dnsElementType);
        writers.Add(dnsElementType, writer);

        // Update the requestClassTypes table so that if a DNSRequestRecord references this type by name, it can resolve an ID.
        foreach (var dnsClass in dnsClasses)
        {
            requestClassTypes.Add(dnsClass.Name ?? dnsElementType.Name, dnsClass.RecordId);
        }
    }

#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
    /// <summary>
    /// Dynamically generates an expression-based serializer (writer) for DNS elements.
    /// </summary>
    /// <typeparam name="T">A <see cref="DNSElement"/>-derived type (e.g., <see cref="DNSHeader"/>, <see cref="DNSResponseRecord"/>).</typeparam>
    /// <param name="dnsElementType">The concrete <see cref="Type"/> to serialize.</param>
    /// <returns>An <see cref="Action{T1,T2}"/> that can write the specified DNS element to the datagram.</returns>
    private Action<Datas, T> CreateReader<T>(Type dnsElementType) where T : DNSElement
    {
        Debug.WriteLine(dnsElementType.FullName);

        // Parameters to the dynamic method: (Datas datas, T dnsElement).
        var datasParameter = Expression.Parameter(typeof(Datas), "datas");
        var elementParameter = Expression.Parameter(typeof(T), "dnsElement");

        // If T != dnsElementType, we need a local variable cast to dnsElementType.
        var elementVariable = Expression.Variable(dnsElementType, "element");
        Expression element;
        var variables = new List<ParameterExpression>();
        var fieldsReaders = new List<Expression>();
        int insertIndex = 0;

        if (typeof(T) == dnsElementType)
        {
            // The parameter is already the correct type, no conversion needed.
            element = elementParameter;
        }
        else
        {
            element = elementVariable;
            variables.Add(elementVariable);

            // Convert from T to the actual dnsElementType at runtime.
            fieldsReaders.Add(Expression.Assign(
                elementVariable,
                Expression.Convert(elementParameter, dnsElementType)));
            insertIndex = 2;
        }

        // For each annotated field/property in the DNS element, generate the appropriate
        // expression-based write calls (e.g., write a ushort, string, byte[], etc.).
        foreach (var field in DNSPacketHelpers.GetDNSFields(dnsElementType))
        {
            // If a field references another field for dynamic length, add some additional logic
            // here (this snippet shows a possible place to handle or manipulate the field length).
            if (field.Attribute.Length is string fieldName)
            {
                var memberType = field.Member.GetTypeOf();

                // Build an expression referencing the length-holding member, then assign it if needed.
                var memberTarget = ExpressionEx.CreateMemberExpression(
                    elementVariable, fieldName, BindingFlags.Public | BindingFlags.NonPublic);

                if (memberType == typeof(string))
                {
                    fieldsReaders.Insert(insertIndex++,
                        Expression.Assign(
                            memberTarget,
                            Expression.Convert(
                                ExpressionEx.CreateMemberExpression(
                                    ExpressionEx.CreateStaticExpression(typeof(Encoding), nameof(Encoding.UTF8)),
                                    nameof(Encoding.UTF8.GetByteCount),
                                    ExpressionEx.CreateMemberExpression(elementVariable, field.Member)
                                ),
                                memberTarget.Type
                            )
                        )
                    );
                }
                else if (memberType == typeof(byte[]))
                {
                    fieldsReaders.Insert(insertIndex++,
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

            // Generate a series of calls to write this field's data.
            Expression[] callExpressions = CreateWriteExpression(
                datasParameter,
                element,
                field.Member,
                field.Attribute);

            fieldsReaders.AddRange(callExpressions);
        }

        // Compile all expressions into a single block:
        // (possible type cast) + (zero or more assignments) + (write calls).
        var expression = Expression.Lambda<Action<Datas, T>>(
            Expression.Block(variables, fieldsReaders),
            "Write" + dnsElementType.Name,
            new[] { datasParameter, elementParameter }
        );

        return expression.Compile();
    }
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields

    /// <summary>
    /// A dictionary mapping known .NET types (e.g., <see cref="ushort"/>, <see cref="T:byte[]"/>)
    /// to a function that generates an expression which writes that type into the <see cref="Datas"/> structure.
    /// </summary>
    private IReadOnlyDictionary<Type, Func<ParameterExpression, Expression, DNSFieldAttribute, Expression[]>> WriterExpressions { get; } =
        new Dictionary<Type, Func<ParameterExpression, Expression, DNSFieldAttribute, Expression[]>>()
        {
            {
                typeof(byte),
                (datasParameter, assignationSource, dnsField)
                    => new[]
                    {
                        ExpressionEx.CreateExpressionCall(
                            datasParameter,
                            nameof(Datas.WriteByte),
                            assignationSource
                        )
                    }
            },
            {
                typeof(ushort),
                (datasParameter, assignationSource, dnsField)
                    => new[]
                    {
                        ExpressionEx.CreateExpressionCall(
                            datasParameter,
                            nameof(Datas.WriteUShort),
                            assignationSource
                        )
                    }
            },
            {
                typeof(uint),
                (datasParameter, assignationSource, dnsField)
                    => new[]
                    {
                        ExpressionEx.CreateExpressionCall(
                            datasParameter,
                            nameof(Datas.WriteUInt),
                            assignationSource
                        )
                    }
            },
            {
                typeof(DNSDomainName),
                (datasParameter, assignationSource, dnsField)
                    => new[]
                    {
                        ExpressionEx.CreateExpressionCall(
                            datasParameter,
                            nameof(Datas.WriteDomainName),
                            assignationSource
                        )
                    }
            },
            {
                typeof(byte[]),
                (datasParameter, assignationSource, dnsField)
                    => dnsField.Length switch
                    {
                        int length => new[]
                        {
                            ExpressionEx.CreateExpressionCall(
                                datasParameter,
                                nameof(Datas.WriteBytes),
                                assignationSource,
                                Expression.Constant(length, typeof(int))
                            )
                        },
                        FieldsSizeOptions options => options switch
                        {
                            FieldsSizeOptions.PrefixedSize1B => new[]
                            {
                                ExpressionEx.CreateExpressionCall(
                                    datasParameter,
                                    nameof(Datas.WriteBytesPrefixed1B),
                                    assignationSource
                                )
                            },
                            FieldsSizeOptions.PrefixedSize2B => new[]
                            {
                                ExpressionEx.CreateExpressionCall(
                                    datasParameter,
                                    nameof(Datas.WriteBytesPrefixed2B),
                                    assignationSource
                                )
                            },
                            FieldsSizeOptions.PrefixedSizeBits1B => new[]
                            {
                                ExpressionEx.CreateExpressionCall(
                                    datasParameter,
                                    nameof(Datas.WriteBytesPrefixedBits1B),
                                    assignationSource
                                )
                            },
                            _ => throw new InvalidOperationException($"{options} is not a valid value")
                        },
                        _ => new[]
                        {
                            ExpressionEx.CreateExpressionCall(
                                datasParameter,
                                nameof(Datas.WriteBytes),
                                assignationSource
                            )
                        }
                    }
            },
            {
                typeof(string),
                (datasParameter, assignationSource, dnsField)
                                          => dnsField.Length switch
                                          {
                                                  int length => new[]
                                                  {
                                                          ExpressionEx.CreateExpressionCall(
                                                                  datasParameter,
                                                                  nameof(Datas.WriteString),
                                                                  assignationSource,
                                                                  Expression.Constant(length, typeof(int))
                                                          )
                                                  },
                        FieldsSizeOptions options => options switch
                        {
                            FieldsSizeOptions.PrefixedSize1B => new[]
                            {
                                ExpressionEx.CreateExpressionCall(
                                    datasParameter,
                                    nameof(Datas.WriteStringPrefixed1B),
                                    assignationSource
                                )
                            },
                            FieldsSizeOptions.PrefixedSize2B => new[]
                            {
                                ExpressionEx.CreateExpressionCall(
                                    datasParameter,
                                    nameof(Datas.WriteStringPrefixed2B),
                                    assignationSource
                                )
                            },
                            FieldsSizeOptions.PrefixedSizeBits1B => new[]
                            {
                                ExpressionEx.CreateExpressionCall(
                                    datasParameter,
                                    nameof(Datas.WriteStringPrefixedBits1B),
                                    assignationSource
                                )
                            },
                            _ => throw new InvalidOperationException($"{options} is not a valid value")
                        },
                        _ => new[]
                        {
                            ExpressionEx.CreateExpressionCall(
                                datasParameter,
                                nameof(Datas.WriteString),
                                assignationSource
                            )
                        }
                    }
            }
        };

    /// <summary>
    /// Builds the expression(s) needed to write a specific field or property of a DNS element
    /// into the datagram, taking into account length specifications and conditions.
    /// </summary>
    /// <param name="datasParameter">The parameter expression referencing the <see cref="Datas"/> instance.</param>
    /// <param name="element">An expression referencing the DNS element being written.</param>
    /// <param name="field">The <see cref="MemberInfo"/> (field or property) to write.</param>
    /// <param name="dnsField">The <see cref="DNSFieldAttribute"/> containing metadata like length or condition.</param>
    /// <returns>An array of expression nodes representing the write operations.</returns>
    private Expression[] CreateWriteExpression(
        ParameterExpression datasParameter,
        Expression element,
        MemberInfo field,
        DNSFieldAttribute dnsField)
    {
        // Build an expression referencing the field or property.
        // e.g., "element.SomeProperty" or "element.someField".
        Type type = field.GetTypeOf();
        Expression assignationSource = field is PropertyInfo
            ? Expression.Property(element, (PropertyInfo)field)
            : Expression.Field(element, (FieldInfo)field);

        // If there's an underlying type mismatch (e.g., a nullable?), convert to the underlying type.
        Type underLyingType = type.GetUnderlyingType();
        if (type != underLyingType)
        {
            assignationSource = Expression.Convert(assignationSource, underLyingType);
        }

        // Look up a suitable expression writer in WriterExpressions, or handle custom conversions.
        Expression[] callExpression = null;
        if (WriterExpressions.TryGetValue(underLyingType, out var getWriterFunction))
        {
            callExpression = getWriterFunction(datasParameter, assignationSource, dnsField);
        }
        else if (ExpressionEx.TryGetConverter(
            new[]
            {
                (typeof(byte[]), assignationSource),
                (typeof(string), assignationSource)
            },
            out var builderToBytes))
        {
            // If there's a known converter from (string / something) → byte[],
            // use the writer for byte[] instead.
            callExpression = WriterExpressions[typeof(byte[])](datasParameter, builderToBytes, dnsField);
        }
        else
        {
            // No recognized type writer found.
            throw new NotSupportedException();
        }

        // If there's a condition on this field, wrap the write calls in an IfThen expression.
        if (dnsField.Condition != null)
        {
            var conditionExpression = DNSExpression.BuildExpression(element, dnsField.Condition);
            callExpression = new[]
            {
                Expression.IfThen(
                    conditionExpression,
                    callExpression.Length == 1
                        ? callExpression[0]
                        : Expression.Block(callExpression)
                )
            };
        }

        return callExpression;
    }

    /// <summary>
    /// An internal class representing the DNS datagram buffer and the current write position.
    /// Also includes a local string position map for DNS name compression.
    /// </summary>
    private sealed class Datas
    {
        /// <summary>
        /// Gets or sets the datagram buffer where DNS data is written.
        /// </summary>
        public byte[] Datagram { get; init; }

        /// <summary>
        /// Gets or sets the current write position in the <see cref="Datagram"/>.
        /// </summary>
        public int Position { get; set; } = 0;

        /// <summary>
        /// Tracks positions of DNS names for DNS name compression (reusing labels with pointers).
        /// </summary>
        public Dictionary<string, ushort> StringsPositions { get; } = new();

        /// <summary>
        /// Holds the current <see cref="Context"/> state if writing a resource record that tracks
        /// data length. When <c>null</c>, length tracking is not in use.
        /// </summary>
        public Context Context { get; set; } = null;

        /// <summary>
        /// Writes a single byte to the datagram, and advances <see cref="Position"/>.
        /// </summary>
        /// <param name="b">The byte to write.</param>
        public void WriteByte(byte b)
        {
            Datagram[Position] = b;
            Position++;
            if (Context != null) { Context.Length++; }
        }

        /// <summary>
        /// Writes the specified byte array to the datagram, calling <see cref="WriteBytes(byte[], int)"/>
        /// with the array's length.
        /// </summary>
        /// <param name="b">The byte array to write.</param>
        public void WriteBytes(byte[] b) => WriteBytes(b, b.Length);

        /// <summary>
        /// Writes a portion (or whole) of a byte array to the datagram.
        /// </summary>
        /// <param name="b">The byte array to write from.</param>
        /// <param name="length">The number of bytes to write. If 0, writes the entire array.</param>
        public void WriteBytes(byte[] b, int length)
        {
            if (length == 0)
                length = b.Length;

            Array.Copy(b, 0, Datagram, Position, length);
            Position += length;

            if (Context != null)
            {
                Context.Length += (ushort)length;
            }
        }

        /// <summary>
        /// Writes a one-byte length prefix followed by the given byte array.
        /// </summary>
        /// <param name="b">The byte array to write.</param>
        public void WriteBytesPrefixed1B(byte[] b)
        {
            WriteByte((byte)b.Length);
            WriteBytes(b, b.Length);
        }

        /// <summary>
        /// Writes a two-byte length prefix (ushort) followed by the given byte array.
        /// </summary>
        /// <param name="b">The byte array to write.</param>
        public void WriteBytesPrefixed2B(byte[] b)
        {
            WriteUShort((ushort)b.Length);
            WriteBytes(b, b.Length);
        }

        /// <summary>
        /// Writes a single byte indicating the length in bits (1B = 8 bits)
        /// followed by the byte array itself.
        /// </summary>
        /// <param name="b">The byte array to write.</param>
        public void WriteBytesPrefixedBits1B(byte[] b)
        {
            WriteByte((byte)(b.Length * 8));
            WriteBytes(b, b.Length);
        }

        /// <summary>
        /// Writes an unsigned 16-bit integer to the datagram in big-endian format.
        /// </summary>
        /// <param name="s">The value to write.</param>
        public void WriteUShort(ushort s)
        {
            WriteUShortAt(Position, s);
            Position += 2;
            if (Context != null) { Context.Length += 2; }
        }

        /// <summary>
        /// Writes an unsigned 16-bit integer at a specific position in the datagram
        /// (does not change <see cref="Position"/>).
        /// </summary>
        /// <param name="position">The datagram index where the data should be written.</param>
        /// <param name="s">The value to write.</param>
        public void WriteUShortAt(int position, ushort s)
        {
            Datagram[position] = (byte)((s >> 8) & 0xFF);
            Datagram[position + 1] = (byte)(s & 0xFF);
        }

        /// <summary>
        /// Writes an unsigned 32-bit integer to the datagram in big-endian format.
        /// </summary>
        /// <param name="i">The value to write.</param>
        public void WriteUInt(uint i)
        {
            WriteUIntAt(Position, i);
            Position += 4;
            if (Context != null) { Context.Length += 4; }
        }

        /// <summary>
        /// Writes an unsigned 32-bit integer at a specific position in the datagram
        /// (does not change <see cref="Position"/>).
        /// </summary>
        /// <param name="position">The datagram index where the data should be written.</param>
        /// <param name="i">The value to write.</param>
        private void WriteUIntAt(int position, uint i)
        {
            Datagram[position] = (byte)((i >> 24) & 0xFF);
            Datagram[position + 1] = (byte)((i >> 16) & 0xFF);
            Datagram[position + 2] = (byte)((i >> 8) & 0xFF);
            Datagram[position + 3] = (byte)(i & 0xFF);
        }

        /// <summary>
        /// Writes a string as UTF-8, constrained to a specific length in bytes.
        /// </summary>
        /// <param name="s">The string to write.</param>
        /// <param name="length">The number of bytes to write from the string's data.</param>
        public void WriteString(string s, int length)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            WriteBytes(bytes, length);
        }

        /// <summary>
        /// Writes a string as UTF-8 in its entirety.
        /// </summary>
        /// <param name="s">The string to write.</param>
        /// <exception cref="NullReferenceException">Thrown if <see cref="Context"/> is null.</exception>
        public void WriteString(string s)
        {
            if (Context == null)
                throw new NullReferenceException("Context must not be null");

            var bytes = Encoding.UTF8.GetBytes(s);
            WriteBytes(bytes);
        }

        /// <summary>
        /// Writes a single-byte length in bits, followed by the UTF-8 content of the given string.
        /// </summary>
        /// <param name="s">The string to write.</param>
        public void WriteStringPrefixedBits1B(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            WriteBytesPrefixedBits1B(bytes);
        }

        /// <summary>
        /// Writes a single-byte prefix indicating the length of the given string, followed by its UTF-8 content.
        /// </summary>
        /// <param name="s">The string to write.</param>
        public void WriteStringPrefixed1B(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            WriteBytesPrefixed1B(bytes);
        }

        /// <summary>
        /// Writes a two-byte prefix (unsigned short) indicating the length of the given string, followed by its UTF-8 content.
        /// </summary>
        /// <param name="s">The string to write.</param>
        public void WriteStringPrefixed2B(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            WriteBytesPrefixed2B(bytes);
        }

        /// <summary>
        /// Writes a DNS domain name using basic DNS label compression. If the domain was previously written,
        /// a pointer is used. Otherwise, each label is written with a length byte, and a trailing 0 byte
        /// denotes the end of the name.
        /// </summary>
        /// <param name="s">The <see cref="DNSDomainName"/> to write.</param>
        public void WriteDomainName(DNSDomainName s)
        {
            var labels = s.Value.ToLowerInvariant().Split('.');
            foreach (var label in labels)
            {
                var bytes = Encoding.ASCII.GetBytes(label);
                WriteByte((byte)bytes.Length);
                WriteBytes(bytes, bytes.Length);
            }
            WriteByte(0);
        }

    }

    /// <summary>
    /// A small data class tracking the length of a DNS RData section while it is being written.
    /// </summary>
    private sealed class Context
    {
        /// <summary>
        /// Gets or sets the total length of the data being written for the current RData block.
        /// </summary>
        public ushort Length { get; set; }
    }

    /// <inheritdoc />
    /// <summary>
    /// Serializes a <see cref="DNSHeader"/> (and its associated records) into a byte array (max 512 bytes by default).
    /// </summary>
    /// <param name="header">The <see cref="DNSHeader"/> to serialize.</param>
    /// <returns>A new <see cref="byte"/> array containing the DNS packet data.</returns>
    public byte[] Write(DNSHeader header)
    {
        Datas datasStructure = new Datas
        {
            Datagram = new byte[512],
            Position = 0
        };

        Write(datasStructure, header);

        // Trim the buffer down to the actual size written.
        var result = new byte[datasStructure.Position];
        Array.Copy(datasStructure.Datagram, result, datasStructure.Position);
        return result;
    }

    /// <summary>
    /// Writes the top-level DNS header and all associated request/response records into
    /// the provided <see cref="Datas"/> structure.
    /// </summary>
    /// <param name="datas">The <see cref="Datas"/> buffer to write into.</param>
    /// <param name="header">The <see cref="DNSHeader"/> to be written.</param>
    private void Write(Datas datas, DNSHeader header)
    {
        // Before writing, ensure the QD/AN/NS/AR counts match the record lists.
        header.QDCount = (ushort)header.Requests.Count;
        header.ANCount = (ushort)header.Responses.Count;
        header.NSCount = (ushort)header.Authorities.Count;
        header.ARCount = (ushort)header.Additionals.Count;

        // Write the header fields.
        WriteHeader(datas, header);

        // Write each request record (substituting the textual record type name with a numeric code).
        foreach (var requestRecord in header.Requests)
        {
            requestRecord.RequestType = requestClassTypes[requestRecord.Type];
            WriteRequestRecord(datas, requestRecord);
        }

        // Write each response record (including authorities and additionals).
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

    /// <summary>
    /// Writes a single <see cref="DNSResponseRecord"/>, then writes its associated RData via
    /// the expression-based writer from <see cref="writers"/>.
    /// </summary>
    /// <param name="datas">The <see cref="Datas"/> buffer to write into.</param>
    /// <param name="responseRecord">The <see cref="DNSResponseRecord"/> to be written.</param>
    private void WriteResponse(Datas datas, DNSResponseRecord responseRecord)
    {
        // First, write the standard DNSResponseRecord fields (NAME, TYPE, CLASS, etc.),
        // which includes leaving space for RDLength.
        WriteResponseRecord(datas, responseRecord);

        // We'll come back and fill RDLength after writing the RData below.
        var middlePosition = datas.Position;

        // Activate the contextual length tracking.
        datas.Context = new Context
        {
            Length = 0
        };

        // Use the stored writer for the specific RData type.
        writers[responseRecord.RData.GetType()](datas, responseRecord.RData);

        var endRecordPosition = datas.Position;

        // Now that RData is written, record how many bytes were written.
        responseRecord.RDLength = datas.Context.Length;
        datas.Context = null;

        // Go back to where we left space for RDLength and patch it in.
        datas.Position = middlePosition - 2;
        datas.WriteUShort(responseRecord.RDLength);

        // Move the position pointer back to the end of RData.
        datas.Position = endRecordPosition;
    }
    /// <summary>
    /// Converts the provided DNS response record into its canonical on-the-wire representation.
    /// </summary>
    /// <param name="record">The DNS response record to serialize.</param>
    /// <returns>The trimmed byte buffer that contains the canonical DNS payload.</returns>
    public byte[] Write(DNSResponseRecord record)
    {
        Datas datas = new Datas { Datagram = new byte[512], Position = 0 };
        WriteResponse(datas, record);
        var result = new byte[datas.Position];
        Array.Copy(datas.Datagram, result, datas.Position);
        return result;
    }

}
