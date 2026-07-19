using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Utils.Arrays;
using Utils.Collections;

namespace Utils.VirtualMachine;

/// <summary>
/// Provides an abstract base class for a virtual processor that interprets and executes
/// instructions from a byte array-based context. Instructions are defined by methods
/// annotated with <see cref="InstructionAttribute"/>.
/// </summary>
/// <typeparam name="T">
/// The type of execution context, which must derive from <see cref="Context"/>.
/// </typeparam>
/// <remarks>
/// This class is not thread-safe. Concurrent calls to <see cref="Execute"/> or
/// <see cref="ExecuteStep"/> on the same instance share a dispatch buffer and must not
/// overlap; use one <see cref="VirtualProcessor{T}"/> instance per thread, or synchronize externally.
/// </remarks>
public abstract class VirtualProcessor<T> where T : Context
{
    // INumberReader methods never change; cache per-type avoids rebuilding on every instantiation.
    // IsAbstract filters to fixed-width methods only; default-implementation methods (LEB128) share
    // return types with ulong/long and would cause duplicate-key conflicts in the dictionary.
    private static readonly Dictionary<Type, MethodInfo> _numberReaderMethods =
        typeof(INumberReader).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                             .Where(m => m.IsAbstract)
                             .ToDictionary(m => m.ReturnType, m => m);

    /// <summary>Number reader configured for the requested endianness.</summary>
    private readonly INumberReader _numberReader;

    /// <summary>Length (in bytes) of the longest registered opcode; bounds the dispatch loop.</summary>
    private int _maxInstructionSize;

    /// <summary>Pre-allocated scratch buffer for opcode accumulation during dispatch; length equals <see cref="_maxInstructionSize"/>.</summary>
    private byte[] _buffer;

    /// <summary>Number of bytes written into <see cref="_buffer"/> for the current dispatch attempt.</summary>
    private int _bufferLength;

    /// <summary>
    /// Fast-path lookup indexed by first byte. Non-null only for single-byte opcodes whose first
    /// byte is not shared by any multi-byte opcode, enabling O(1) dispatch without a loop.
    /// Rebuilt whenever the instruction set changes (construction or <see cref="RegisterInstruction"/>).
    /// </summary>
    private (string Name, InstructionDelegate Handler)?[] _fastLookup = new (string, InstructionDelegate)?[256];

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
    /// Gets or sets an optional inspector that receives a notification before each instruction is
    /// dispatched. Setting this to <see langword="null"/> (the default) disables inspection with
    /// zero per-instruction overhead.
    /// </summary>
    public IVmInspector<T>? Inspector { get; set; }

    /// <summary>
    /// A set of instruction-pointer addresses that trigger
    /// <see cref="IVmInspector{T}.OnBreakpoint"/> when execution reaches them.
    /// Breakpoints are only checked when <see cref="Inspector"/> is non-null.
    /// </summary>
    public HashSet<int> Breakpoints { get; } = [];

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
        _buffer = new byte[_maxInstructionSize];
        RebuildFastLookup();
    }

    /// <summary>
    /// Discovers all instruction methods via reflection, collecting them into a dictionary
    /// keyed by their associated byte sequences.
    /// </summary>
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
            if (parameters.Length > 0 && parameters[0].ParameterType != typeof(T)) continue;
            InstructionDelegate instructionDelegate;
            if (parameters.Length == 0)
            {
                // Parameterless method (e.g. NOOP): compile a wrapper lambda that discards the context
                // so the signature matches InstructionDelegate.
                var ctxParam = Expression.Parameter(typeof(T), "context");
                var callExpr = Expression.Call(Expression.Constant(this), method);
                instructionDelegate = Expression.Lambda<InstructionDelegate>(callExpr, ctxParam).Compile();
            }
            else if (parameters.Length == 1)
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
                // Clone the attribute's opcode into owned immutable storage so that external
                // mutations of the source array cannot corrupt the dictionary's hash buckets.
                byte[] ownedOpcode = (byte[])attr.Instruction.Clone();
                CheckPrefixConflict(result, ownedOpcode);
                result.Add(ownedOpcode, (attr.Name, instructionDelegate));
                _maxInstructionSize = Math.Max(_maxInstructionSize, ownedOpcode.Length);
            }
        }

        return result;
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <paramref name="newOpcode"/> is a proper prefix
    /// of an already-registered opcode, or when any registered opcode is a proper prefix of
    /// <paramref name="newOpcode"/>. Prefix ambiguity makes one of the opcodes permanently
    /// unreachable in the dispatch loop.
    /// </summary>
    private static void CheckPrefixConflict(
        Dictionary<IReadOnlyCollection<byte>, (string Name, InstructionDelegate Handler)> existing,
        byte[] newOpcode)
    {
        foreach (var key in existing.Keys)
        {
            var keyList = key.ToList();
            int minLen = Math.Min(keyList.Count, newOpcode.Length);
            bool sharedPrefix = true;
            for (int i = 0; i < minLen; i++)
            {
                if (keyList[i] != newOpcode[i]) { sharedPrefix = false; break; }
            }
            if (sharedPrefix && keyList.Count != newOpcode.Length)
                throw new ArgumentException(
                    $"Opcode [{string.Join(", ", newOpcode.Select(b => $"0x{b:X2}"))}] conflicts with " +
                    $"already-registered opcode [{string.Join(", ", keyList.Select(b => $"0x{b:X2}"))}]: " +
                    "one is a proper prefix of the other. Prefix conflicts make the longer opcode unreachable.",
                    "opcode");
        }
    }

    /// <summary>
    /// Rebuilds <see cref="_fastLookup"/> after any change to the instruction set.
    /// A slot is populated only when all opcodes sharing that first byte are exactly one byte long,
    /// making the dispatch unambiguous without reading additional bytes.
    /// </summary>
    private void RebuildFastLookup()
    {
        // First pass: mark which first-bytes have at least one multi-byte opcode.
        bool[] hasMultiByteExtension = new bool[256];
        foreach (var key in InstructionsSet.Keys)
        {
            if (key.Count > 1)
                hasMultiByteExtension[key.First()] = true;
        }

        // Second pass: populate fast-path slots for unambiguous single-byte opcodes.
        var lookup = new (string Name, InstructionDelegate Handler)?[256];
        foreach (var kv in InstructionsSet)
        {
            var key = kv.Key;
            if (key.Count != 1) continue;
            byte firstByte = key.First();
            if (!hasMultiByteExtension[firstByte])
                lookup[firstByte] = (kv.Value.Name, kv.Value.Handler);
        }
        _fastLookup = lookup;
    }

    #region Reading Methods

    /// <summary>
    /// Reads a single byte from the context using the configured <see cref="INumberReader"/>.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <returns>The byte read from the data stream.</returns>
    protected byte ReadByte(Context context) => _numberReader.ReadByte(context);

    /// <summary>
    /// Reads a single signed byte from the context using the configured <see cref="INumberReader"/>.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <returns>The signed byte read from the data stream.</returns>
    protected sbyte ReadSByte(Context context) => _numberReader.ReadSByte(context);

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
    /// Called before each instruction is dispatched during <see cref="Execute"/> and
    /// <see cref="ExecuteStep"/>. The default implementation does nothing.
    /// Override to add breakpoints, tracing, or profiling without modifying the dispatch loop.
    /// At the time of the call, <see cref="Context.InstructionPointer"/> points to the
    /// first byte of the instruction about to execute.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    protected virtual void OnStep(T context) { }

    /// <summary>
    /// Executes all instructions until the end of the data stream, until
    /// <see cref="Context.InstructionPointer"/> becomes negative (program termination signal),
    /// or until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Token that can stop execution between instructions.</param>
    /// <exception cref="VirtualProcessorException">Thrown on an unknown opcode sequence.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public void Execute(T context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        while (context.InstructionPointer >= 0 && context.InstructionPointer < context.Data.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OnStep(context);
            TryDispatch(context);
        }
    }

    /// <summary>
    /// Executes exactly one instruction at the current instruction pointer.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <returns>
    /// <see langword="true"/> if an instruction was dispatched;
    /// <see langword="false"/> if the instruction pointer is past the end of the data stream
    /// or is negative (program termination signal).
    /// </returns>
    /// <exception cref="VirtualProcessorException">Thrown on an unknown opcode sequence.</exception>
    public bool ExecuteStep(T context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.InstructionPointer < 0 || context.InstructionPointer >= context.Data.Length) return false;
        OnStep(context);
        TryDispatch(context);
        return true;
    }

    /// <summary>
    /// Registers an instruction handler at runtime, supplementing or overriding
    /// the methods discovered via <see cref="InstructionAttribute"/>.
    /// </summary>
    /// <param name="opcode">Byte sequence identifying the instruction.</param>
    /// <param name="name">Human-readable name used in diagnostics. Must not be <see langword="null"/>, empty, or whitespace.</param>
    /// <param name="handler">Delegate invoked when the opcode is matched.</param>
    /// <param name="overwrite">
    /// When <see langword="true"/>, replaces an existing registration for the same opcode.
    /// When <see langword="false"/> (default), throws if the opcode is already registered.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="opcode"/> is empty, when <paramref name="name"/> is empty or whitespace-only,
    /// or when the opcode is already registered and <paramref name="overwrite"/> is <see langword="false"/>.
    /// </exception>
    public void RegisterInstruction(byte[] opcode, string name, Action<T> handler, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(opcode);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(handler);
        if (opcode.Length == 0) throw new ArgumentException("Opcode cannot be empty.", nameof(opcode));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Instruction name must not be empty or whitespace.", nameof(name));
        if (!overwrite && InstructionsSet.ContainsKey(opcode))
            throw new ArgumentException(
                $"An instruction with opcode [{string.Join(", ", opcode.Select(b => $"0x{b:X2}"))}] is already registered.",
                nameof(opcode));

        // Clone into owned storage so that external mutations of the source array cannot corrupt
        // dictionary hash buckets or dispatch tables after registration.
        byte[] ownedOpcode = (byte[])opcode.Clone();
        // Always enforce prefix-conflict rules. CheckPrefixConflict only throws when lengths differ,
        // so replacing an exact opcode (overwrite: true) is allowed while cross-length conflicts are
        // still rejected — a conflict with a different-length opcode is invalid regardless of overwrite.
        CheckPrefixConflict(InstructionsSet, ownedOpcode);

        InstructionsSet[ownedOpcode] = (name, ctx => handler(ctx));
        if (ownedOpcode.Length > _maxInstructionSize)
        {
            _maxInstructionSize = ownedOpcode.Length;
            Array.Resize(ref _buffer, _maxInstructionSize);
        }
        RebuildFastLookup();
    }

    /// <summary>
    /// Shared dispatch core: resolves and invokes the instruction at the current pointer.
    /// Uses a pre-built fast-path array for unambiguous single-byte opcodes (O(1)) and falls
    /// back to a byte-by-byte loop for multi-byte opcodes.
    /// Reuses the instance-level <see cref="_buffer"/> array; not thread-safe.
    /// </summary>
    /// <exception cref="VirtualProcessorException">
    /// Thrown when no instruction matches the bytes at the current position, or when a matched
    /// instruction's handler raises <see cref="IndexOutOfRangeException"/> or
    /// <see cref="ArgumentOutOfRangeException"/> (indicating truncated operand data).
    /// </exception>
    private void TryDispatch(T context)
    {
        int instructionStart = context.InstructionPointer;

        // Fast path: O(1) dispatch for single-byte opcodes with no multi-byte peers.
        byte firstByte = context.Data.Span[instructionStart];
        if (_fastLookup[firstByte] is { } fast)
        {
            // IP has not been advanced yet: expected value after the callback is still instructionStart.
            NotifyInspector(context, instructionStart, instructionStart, fast.Name);
            // If the inspector redirected execution (e.g. a breakpoint handler jumped away),
            // skip this instruction entirely to honour the new instruction pointer.
            if (context.InstructionPointer != instructionStart) return;
            context.InstructionPointer++;
            try
            {
                fast.Handler(context);
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
            {
                throw new VirtualProcessorException(instructionStart, new ArraySegment<byte>([firstByte]), fast.Name, ex);
            }
            return;
        }

        // Slow path: accumulate bytes until a match is found or all options are exhausted.
        _bufferLength = 0;
        for (int i = 0; i < _maxInstructionSize && context.InstructionPointer < context.Data.Length; i++)
        {
            _buffer[_bufferLength++] = ReadByte(context);
            var segment = new ArraySegment<byte>(_buffer, 0, _bufferLength);
            if (InstructionsSet.TryGetValue(segment, out var entry))
            {
                // IP has already advanced past the opcode bytes: pass the post-opcode position
                // as the expected value so NotifyInspector can distinguish a genuine redirect
                // from the normal advancement due to reading a multi-byte opcode.
                int afterOpcodeIp = context.InstructionPointer;
                NotifyInspector(context, instructionStart, afterOpcodeIp, entry.Name);
                // Same guard: inspector may redirect before operands are consumed.
                if (context.InstructionPointer != afterOpcodeIp) return;
                try
                {
                    entry.Handler(context);
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
                {
                    // INumberReader methods throw these when the byte stream ends before all operand bytes are read.
                    throw new VirtualProcessorException(instructionStart, segment, entry.Name, ex);
                }
                return;
            }
        }
        throw new VirtualProcessorException(instructionStart, new ArraySegment<byte>(_buffer, 0, _bufferLength));
    }

    /// <summary>
    /// Notifies <see cref="Inspector"/> (when set) before an instruction runs, and additionally
    /// calls <see cref="IVmInspector{T}.OnBreakpoint"/> when the address is in <see cref="Breakpoints"/>.
    /// If <see cref="IVmInspector{T}.OnBreakpoint"/> redirects the instruction pointer, this method
    /// returns without calling <see cref="IVmInspector{T}.BeforeInstruction"/> so that the callback
    /// does not receive stale instruction metadata for the old address.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <param name="address">Address of the instruction (first byte of the opcode).</param>
    /// <param name="expectedInstructionPointer">
    /// The value <see cref="Context.InstructionPointer"/> is expected to hold when no redirect has
    /// occurred. For the fast dispatch path this equals <paramref name="address"/> (IP not yet
    /// advanced); for the slow path it equals the position immediately after the last opcode byte
    /// was consumed. Any other value after <see cref="IVmInspector{T}.OnBreakpoint"/> is treated
    /// as a deliberate redirect.
    /// </param>
    /// <param name="instructionName">Human-readable instruction name for diagnostics.</param>
    private void NotifyInspector(T context, int address, int expectedInstructionPointer, string instructionName)
    {
        if (Inspector is not { } inspector) return;
        if (Breakpoints.Count > 0 && Breakpoints.Contains(address))
        {
            inspector.OnBreakpoint(context, address, instructionName);
            // If OnBreakpoint redirected execution, skip BeforeInstruction for the old instruction
            // so that the callback does not observe contradictory metadata.
            if (context.InstructionPointer != expectedInstructionPointer) return;
        }
        inspector.BeforeInstruction(context, address, instructionName);
    }
}

/// <summary>
/// Represents the base execution context used by <see cref="VirtualProcessor{T}"/>.
/// </summary>
public abstract class Context
{
    /// <summary>
    /// Gets the raw byte data to be processed by the virtual processor.
    /// The data is read-only; instruction handlers cannot modify the instruction stream.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// Gets or sets the current instruction pointer indicating the byte offset into <see cref="Data"/>.
    /// Setting this to a negative value (typically <c>-1</c>) signals program termination to the
    /// <see cref="VirtualProcessor{T}"/> execution loop.
    /// </summary>
    public int InstructionPointer { get; set; }

    /// <summary>
    /// Signals program termination by setting <see cref="InstructionPointer"/> to <c>-1</c>.
    /// The <see cref="VirtualProcessor{T}"/> execution loop stops before the next instruction.
    /// Prefer this over setting <see cref="InstructionPointer"/> to <see cref="Data"/>.<see cref="ReadOnlyMemory{T}.Length"/>
    /// so that intent is explicit and the mechanism can evolve without changing every HALT handler.
    /// </summary>
    public void Terminate() => InstructionPointer = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="Context"/> class with the given data buffer.
    /// The buffer is copied so that subsequent mutations of the caller's array do not affect
    /// the instruction stream seen during execution.
    /// </summary>
    /// <param name="data">The byte data containing all instructions or data to process.</param>
    protected Context(ReadOnlyMemory<byte> data)
    {
        Data = data.ToArray();
    }
}

/// <summary>
/// A default context implementation providing a bounded object operand stack.
/// </summary>
public class DefaultContext : Context
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultContext"/> class with the given data buffer
    /// and the default operand-stack depth limit (<see cref="BoundedStack{T}.DefaultMaxDepth"/>).
    /// </summary>
    /// <param name="data">The byte data containing all instructions or data to process.</param>
    public DefaultContext(ReadOnlyMemory<byte> data) : base(data)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultContext"/> class with the given data buffer
    /// and a custom operand-stack depth limit.
    /// </summary>
    /// <param name="data">The byte data containing all instructions or data to process.</param>
    /// <param name="maxOperandStackDepth">
    /// Maximum number of values the operand stack may hold simultaneously.
    /// Defaults to <see cref="BoundedStack{T}.DefaultMaxDepth"/> when this constructor is not used.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxOperandStackDepth"/> is less than one.</exception>
    public DefaultContext(ReadOnlyMemory<byte> data, int maxOperandStackDepth) : base(data)
    {
        Stack = new BoundedStack<object>(maxOperandStackDepth);
    }

    /// <summary>
    /// A bounded operand stack for temporary values during instruction execution.
    /// Push throws <see cref="InvalidOperationException"/> when the depth limit is reached;
    /// Pop and Peek throw when the stack is empty.
    /// </summary>
    public BoundedStack<object> Stack { get; } = new BoundedStack<object>();
}
