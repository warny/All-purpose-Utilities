using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
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

			var numberReaderType = typeof(INumberReader);
			var numberReaderTypeMethods = numberReaderType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
				.ToDictionary(m=>m.ReturnType, m=>m);

            var methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var method in methods)
            {
                var instructionAttributes = method.GetCustomAttributes(typeof(InstructionAttribute), true);
                if (instructionAttributes.IsNullOrEmptyCollection())
                    continue;

				var parameters = method.GetParameters();
				if (parameters[0].ParameterType != typeof(T)) continue;
				InstructionDelegate instructionDelegate;
				if (parameters.Length == 1)
				{
					instructionDelegate = (InstructionDelegate)method.CreateDelegate(typeof(InstructionDelegate), this);
				}
				else
				{
					var contextParameter = Expression.Parameter(typeof(T), "context");
					var numberReaderExpression = Expression.Constant(_numberReader);

					List<Expression> methodParameters = [
						contextParameter
					];

					foreach (var parameter in parameters.Skip(1))
					{
						var readerMethod = numberReaderTypeMethods[parameter.ParameterType];
						var methodCallExpression = Expression.Call(numberReaderExpression, readerMethod, [contextParameter]);
						methodParameters.Add(methodCallExpression);
					}

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

        #endregion

        /// <summary>
        /// Executes instructions by reading the context's data byte-by-byte, matching against known instructions,
        /// and invoking the corresponding handler delegate.
        /// </summary>
        /// <param name="context">The execution context containing the data and instruction pointer.</param>
        /// <exception cref="VirtualProcessorException">
        /// Thrown when an unknown instruction byte sequence is encountered.
        /// </exception>
        public void Execute(T context)
        {
            // Buffer for building up the current instruction byte sequence
            var currentInstruction = new List<byte>(_maxInstructionSize);

            while (context.InstructionPointer < context.Data.Length)
            {
                bool instructionFound = false;
                currentInstruction.Clear();

                // Read up to the max instruction size (or until data is exhausted)
                for (int i = 0; i < _maxInstructionSize && context.InstructionPointer < context.Data.Length; i++)
                {
                    currentInstruction.Add(ReadByte(context));

                    if (InstructionsSet.TryGetValue(currentInstruction, out var entry))
                    {
                        // Optionally log or trace the instruction name:
                        // Console.WriteLine($"Executing instruction: {entry.Name}");
                        entry.Handler(context);
                        instructionFound = true;
                        break;
                    }
                }

                if (!instructionFound)
                {
                    throw new VirtualProcessorException(
                        $"Unknown instruction encountered at InstructionPointer={context.InstructionPointer}."
                    );
                }
            }
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
