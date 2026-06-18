using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Utils.Arrays;
using Utils.Collections;

namespace Utils.VirtualMachine
{
    /// <summary>
    /// Provides an abstract base class for a virtual processor that interprets and executes
    /// instructions from a byte array-based context. Instructions are defined by methods
    /// annotated with <see cref="InstructionAttribute"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of execution context, which must derive from <see cref="Context"/>.
    /// </typeparam>
    public abstract class VirtualProcessor<T> where T : Context
    {
        // INumberReader methods never change; cache per-type avoids rebuilding on every instantiation.
        // IsAbstract filters to fixed-width methods only; default-implementation methods (LEB128) share
        // return types with ulong/long and would cause duplicate-key conflicts in the dictionary.
        private static readonly Dictionary<Type, MethodInfo> _numberReaderMethods =
            typeof(INumberReader).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(m => m.IsAbstract)
                                 .ToDictionary(m => m.ReturnType, m => m);

        private readonly INumberReader _numberReader;
        private int _maxInstructionSize;

        /// <summary>
        /// Represents a delegate to handle an instruction, given the current context.
        /// </summary>
        /// <param name="context">An instance of the execution context.</param>
        public delegate void InstructionDelegate(T context);

        /// <summary>
        /// A dictionary mapping each instruction byte sequence to a (name, delegate) pair.
        /// The equality comparer for keys is configured to handle byte-array comparison.
        /// </summary>
        protected Dictionary<IReadOnlyCollection<byte>, (string Name, InstructionDelegate Handler)> InstructionsSet { get; }

        /// <summary>
        /// Gets a read-only enumeration of all registered instructions, including those discovered via
        /// <see cref="InstructionAttribute"/> and those added with <see cref="RegisterInstruction"/>.
        /// </summary>
        public IEnumerable<(IReadOnlyCollection<byte> Opcode, string Name)> Instructions
            => InstructionsSet.Select(kv => (kv.Key, kv.Value.Name));

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualProcessor{T}"/> class,
        /// specifying whether the byte data should be interpreted as little-endian.
        /// </summary>
        /// <param name="littleEndian">
        /// If <see langword="true"/>, uses little-endian byte ordering; otherwise, uses big-endian.
        /// Defaults to <see langword="true"/>.
        /// </param>
        protected VirtualProcessor(bool littleEndian = true)
        {
            _numberReader = NumberReader.GetReader(littleEndian);
            InstructionsSet = DiscoverInstructionSet();
        }

        /// <summary>
        /// Discovers all instruction methods via reflection, collecting them into a dictionary
        /// keyed by their associated byte sequences.
        /// </summary>
        /// <returns>
        /// A dictionary where the key is the instruction byte sequence, and the value is a tuple
        /// containing the instruction name and its delegate handler.
        /// </returns>
        private Dictionary<IReadOnlyCollection<byte>, (string Name, InstructionDelegate Handler)> DiscoverInstructionSet()
        {
            var result = new Dictionary<IReadOnlyCollection<byte>, (string, InstructionDelegate)>(
                ArrayEqualityComparers.Byte
            );

            var methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var method in methods)
            {
                if (!method.IsDefined(typeof(InstructionAttribute), true)) continue;
                var instructionAttributes = method.GetCustomAttributes(typeof(InstructionAttribute), true);

                var parameters = method.GetParameters();
                if (parameters[0].ParameterType != typeof(T)) continue;
                InstructionDelegate instructionDelegate;
                if (parameters.Length == 1)
                {
                    instructionDelegate = (InstructionDelegate)method.CreateDelegate(typeof(InstructionDelegate), this);
                }
                else
                {
                    // Build an expression-tree delegate that reads each operand from the INumberReader
                    // and passes them to the instruction method. This compiles to a direct call with no
                    // boxing or reflection overhead at dispatch time.
                    var contextParameter = Expression.Parameter(typeof(T), "context");
                    var numberReaderExpression = Expression.Constant(_numberReader);

                    List<Expression> methodParameters = [
                        contextParameter
                    ];

                    foreach (var parameter in parameters.Skip(1))
                    {
                        // Map each parameter type to the INumberReader method returning that type
                        // (e.g. short → ReadInt16, int → ReadInt32). Lookup table cached in _numberReaderMethods.
                        var readerMethod = _numberReaderMethods[parameter.ParameterType];
                        var methodCallExpression = Expression.Call(numberReaderExpression, readerMethod, [contextParameter]);
                        methodParameters.Add(methodCallExpression);
                    }

                    // Final lambda: (T context) => this.Method(context, reader.ReadXxx(context), ...)
                    var expression = Expression.Lambda<InstructionDelegate>(Expression.Call(Expression.Constant(this), method, methodParameters.ToArray()), [contextParameter]);
                    instructionDelegate = expression.Compile();
                }

                foreach (InstructionAttribute attr in instructionAttributes.OfType<InstructionAttribute>())
                {
                    result.Add(attr.Instruction, (attr.Name, instructionDelegate));
                    _maxInstructionSize = Math.Max(_maxInstructionSize, attr.Instruction.Length);
                }


            }

            return result;
        }

        #region Reading Methods

        /// <summary>
        /// Reads a single byte from the context using the configured <see cref="INumberReader"/>.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>The byte read from the data stream.</returns>
        protected byte ReadByte(Context context) => _numberReader.ReadByte(context);

        /// <summary>
        /// Reads a 16-bit signed integer (short) from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>A 16-bit signed integer.</returns>
        protected short ReadInt16(Context context) => _numberReader.ReadInt16(context);

        /// <summary>
        /// Reads a 32-bit signed integer (int) from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>A 32-bit signed integer.</returns>
        protected int ReadInt32(Context context) => _numberReader.ReadInt32(context);

        /// <summary>
        /// Reads a 64-bit signed integer (long) from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>A 64-bit signed integer.</returns>
        protected long ReadInt64(Context context) => _numberReader.ReadInt64(context);

        /// <summary>
        /// Reads a 16-bit unsigned integer (ushort) from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>A 16-bit unsigned integer.</returns>
        protected ushort ReadUInt16(Context context) => _numberReader.ReadUInt16(context);

        /// <summary>
        /// Reads a 32-bit unsigned integer (uint) from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>A 32-bit unsigned integer.</returns>
        protected uint ReadUInt32(Context context) => _numberReader.ReadUInt32(context);

        /// <summary>
        /// Reads a 64-bit unsigned integer (ulong) from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>A 64-bit unsigned integer.</returns>
        protected ulong ReadUInt64(Context context) => _numberReader.ReadUInt64(context);

        /// <summary>
        /// Reads a 32-bit floating-point number (float) from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>A 32-bit floating-point number.</returns>
        protected float ReadSingle(Context context) => _numberReader.ReadSingle(context);

        /// <summary>
        /// Reads a 64-bit floating-point number (double) from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>A 64-bit floating-point number.</returns>
        protected double ReadDouble(Context context) => _numberReader.ReadDouble(context);

        /// <summary>
        /// Reads an unsigned LEB128-encoded integer from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>The unsigned integer decoded from the LEB128 byte sequence.</returns>
        protected ulong ReadULEB128(Context context) => _numberReader.ReadULEB128(context);

        /// <summary>
        /// Reads a signed LEB128-encoded integer from the context.
        /// </summary>
        /// <param name="context">The current execution context.</param>
        /// <returns>The signed integer decoded from the LEB128 byte sequence.</returns>
        protected long ReadSLEB128(Context context) => _numberReader.ReadSLEB128(context);

        #endregion

        /// <summary>
        /// Executes all instructions until the end of the data stream or until
        /// <paramref name="cancellationToken"/> is cancelled.
        /// </summary>
        /// <param name="context">The execution context.</param>
        /// <param name="cancellationToken">Token that can stop execution between instructions.</param>
        /// <exception cref="VirtualProcessorException">Thrown on an unknown opcode sequence.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public void Execute(T context, CancellationToken cancellationToken = default)
        {
            var buffer = new List<byte>(_maxInstructionSize);
            while (context.InstructionPointer < context.Data.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryDispatch(context, buffer);
            }
        }

        /// <summary>
        /// Executes exactly one instruction at the current instruction pointer.
        /// </summary>
        /// <param name="context">The execution context.</param>
        /// <returns>
        /// <see langword="true"/> if an instruction was dispatched;
        /// <see langword="false"/> if the end of the data stream was already reached.
        /// </returns>
        /// <exception cref="VirtualProcessorException">Thrown on an unknown opcode sequence.</exception>
        public bool ExecuteStep(T context)
        {
            if (context.InstructionPointer >= context.Data.Length) return false;
            var buffer = new List<byte>(_maxInstructionSize);
            TryDispatch(context, buffer);
            return true;
        }

        /// <summary>
        /// Registers an instruction handler at runtime, supplementing or overriding
        /// the methods discovered via <see cref="InstructionAttribute"/>.
        /// </summary>
        /// <param name="opcode">Byte sequence identifying the instruction.</param>
        /// <param name="name">Human-readable name used in diagnostics.</param>
        /// <param name="handler">Delegate invoked when the opcode is matched.</param>
        public void RegisterInstruction(byte[] opcode, string name, Action<T> handler)
        {
            ArgumentNullException.ThrowIfNull(opcode);
            ArgumentNullException.ThrowIfNull(handler);
            if (opcode.Length == 0) throw new ArgumentException("Opcode cannot be empty.", nameof(opcode));
            if (InstructionsSet.ContainsKey(opcode))
                throw new ArgumentException(
                    $"An instruction with opcode [{string.Join(", ", opcode.Select(b => $"0x{b:X2}"))}] is already registered.",
                    nameof(opcode));

            InstructionsSet.Add(opcode, (name ?? string.Empty, ctx => handler(ctx)));
            _maxInstructionSize = Math.Max(_maxInstructionSize, opcode.Length);
        }

        /// <summary>
        /// Shared dispatch core: reads bytes one at a time and invokes the matching handler.
        /// Reuses <paramref name="buffer"/> across iterations (caller must call Clear before each call).
        /// </summary>
        /// <exception cref="VirtualProcessorException">
        /// Thrown when no instruction matches the bytes at the current position, or when a matched
        /// instruction's handler raises <see cref="IndexOutOfRangeException"/> or
        /// <see cref="ArgumentOutOfRangeException"/> (indicating truncated operand data).
        /// </exception>
        private void TryDispatch(T context, List<byte> buffer)
        {
            buffer.Clear();
            int instructionStart = context.InstructionPointer;
            for (int i = 0; i < _maxInstructionSize && context.InstructionPointer < context.Data.Length; i++)
            {
                buffer.Add(ReadByte(context));
                if (InstructionsSet.TryGetValue(buffer, out var entry))
                {
                    try
                    {
                        entry.Handler(context);
                    }
                    catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
                    {
                        // INumberReader methods throw these when the byte stream ends before all operand bytes are read.
                        throw new VirtualProcessorException(instructionStart, buffer, ex);
                    }
                    return;
                }
            }
            throw new VirtualProcessorException(instructionStart, buffer);
        }
    }

    /// <summary>
    /// Represents the base execution context used by <see cref="VirtualProcessor{T}"/>.
    /// </summary>
    public abstract class Context
    {
        /// <summary>
        /// Gets the raw byte data to be processed by the virtual processor.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets or sets the current instruction pointer indicating the byte offset into <see cref="Data"/>.
        /// </summary>
        public int InstructionPointer { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Context"/> class with the given data buffer.
        /// </summary>
        /// <param name="data">The byte array containing all instructions or data to process.</param>
        protected Context(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }
    }

    /// <summary>
    /// A default context implementation providing an object stack.
    /// </summary>
    public class DefaultContext : Context
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultContext"/> class with the given data buffer.
        /// </summary>
        /// <param name="data">The byte array containing all instructions or data to process.</param>
        public DefaultContext(byte[] data) : base(data)
        {
        }

        /// <summary>
        /// A stack of objects that can be used during instruction execution for temporary data storage.
        /// </summary>
        public Stack<object> Stack { get; } = new Stack<object>();
    }
}
