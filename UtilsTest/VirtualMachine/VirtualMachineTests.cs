using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Utils.Arrays;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine
{
    /// <summary>
    /// Minimal machine whose NOP instruction has no parameters at all (not even a context).
    /// Validates that parameterless [Instruction] methods are dispatched correctly.
    /// </summary>
    public class NoopTestMachine : VirtualProcessor<DefaultContext>
    {
        public bool NopExecuted;

        [Instruction("NOP", 0xF0)]
        void Nop() => NopExecuted = true;
    }

    public class LEB128TestMachine : VirtualProcessor<DefaultContext>
    {
        [Instruction("PUSH_ULEB128", 0x20)]
        void PushULEB128(DefaultContext context) => context.Stack.Push(ReadULEB128(context));

        [Instruction("PUSH_SLEB128", 0x21)]
        void PushSLEB128(DefaultContext context) => context.Stack.Push(ReadSLEB128(context));
    }

    public class SByteTestMachine : VirtualProcessor<DefaultContext>
    {
        [Instruction("PUSH_SBYTE", 0x30)]
        void PushSByte(DefaultContext context, sbyte b) => context.Stack.Push(b);
    }

    public class IntMachine : VirtualProcessor<TypedStackContext<int>>
    {
        [Instruction("PUSH_INT", 0x40)]
        void Push(TypedStackContext<int> context, int v) => context.Stack.Push(v);

        [Instruction("ADD_INT", 0x41)]
        void Add(TypedStackContext<int> context)
        {
            var b = context.Stack.Pop();
            var a = context.Stack.Pop();
            context.Stack.Push(a + b);
        }
    }

    public class TestMachine : VirtualProcessor<DefaultContext>
    {
        // Two [Instruction] attributes on the same method (verifies AllowMultiple = true).
        [Instruction("PUSH_BYTE",  0x01, 0x01)]
        [Instruction("PUSH_BYTE2", 0x01, 0x03)]
        void PushByte(DefaultContext context, byte b1)
        {
            context.Stack.Push(b1);
        }

        [Instruction("PUSH", 0x01, 0x02)]
        void PushShort(DefaultContext context, short b1)
        {
            context.Stack.Push(b1);
        }

        [Instruction("POP", 0x02)]
        void Pop(DefaultContext context)
        {
            context.Stack.Pop();
        }

        [Instruction("ADD", 0X10, 0x01)]
        void Add(DefaultContext context)
        {
            var op2 = context.Stack.Pop();
            var op1 = context.Stack.Pop();

            if (op1 is byte bop1 && op2 is byte bop2)
            {
                var res = bop1 + bop2;
                context.Stack.Push((byte)res);
            }
            else
            {
                short sop1 = (short)(op1 as short? ?? op1 as byte?);
                short sop2 = (short)(op2 as short? ?? op2 as byte?);
                var res = sop1 + sop2;
                context.Stack.Push((short)res);
            }
        }

        [Instruction("SUB", 0X10, 0x02)]
        void Substract(DefaultContext context)
        {
            var op2 = (byte)context.Stack.Pop();
            var op1 = (byte)context.Stack.Pop();
            var res = op1 - op2;
            context.Stack.Push((byte)res);
        }
    }


    [TestClass]
    public class VirtualMachineTests
    {
        [TestMethod]
        public void Test1()
        {
            byte[] instructions = [ 
                //Push 0x10
                0x01, 0x01, 0x10, 
                //Push 0x01
                0x01, 0x01, 0x01
            ];

            var context = new DefaultContext(instructions);
            TestMachine machine = new TestMachine();
            machine.Execute(context);

            var result = context.Stack.OfType<byte>().ToArray();
            Assert.IsTrue(ArrayEqualityComparers.Byte.Equals([0x01, 0x10], result));
        }

        [TestMethod]
        public void Test2()
        {
            byte[] instructions = [ 
                //Push 0x10
                0x01, 0x01, 0x10, 
                //Push 0x01
                0x01, 0x01, 0x01, 
                //Add 
                0x10, 0x01
            ];

            var context = new DefaultContext(instructions);
            TestMachine machine = new TestMachine();
            machine.Execute(context);

            var result = context.Stack.OfType<byte>().ToArray();
            Assert.IsTrue(ArrayEqualityComparers.Byte.Equals([0x11], result));
        }

        [TestMethod]
        public void Test3()
        {
            byte[] instructions = [ 
                //Push 0x10
                0x01, 0x01, 0x10, 
                //Push 0x01
                0x01, 0x01, 0x01, 
                //Substract 
                0x10, 0x02
            ];

            var context = new DefaultContext(instructions);
            TestMachine machine = new TestMachine();
            machine.Execute(context);

            var result = context.Stack.OfType<byte>().ToArray();
            Assert.IsTrue(ArrayEqualityComparers.Byte.Equals([0x0F], result));
        }

        [TestMethod]
        public void Test4()
        {
            byte[] instructions = [ 
                //Push 0x10
                0x01, 0x02, 0x10, 0x00,
                //Push 0x01
                0x01, 0x02, 0x01, 0x00,
                //Add 
                0x10, 0x01
            ];

            var context = new DefaultContext(instructions);
            TestMachine machine = new TestMachine();
            machine.Execute(context);

            var result = context.Stack.OfType<short>().ToArray();
            Assert.IsTrue(ArrayEqualityComparers.Int16.Equals([0x11], result));
        }

        // ── ExecuteStep ───────────────────────────────────────────────────────

        [TestMethod]
        public void ExecuteStep_RunsOneInstructionAtATime()
        {
            // PUSH 0x10, then PUSH 0x01 — two instructions.
            byte[] instructions = [0x01, 0x01, 0x10,  0x01, 0x01, 0x01];
            var context = new DefaultContext(instructions);
            TestMachine machine = new TestMachine();

            Assert.IsTrue(machine.ExecuteStep(context));   // first PUSH
            Assert.AreEqual(1, context.Stack.Count);
            Assert.IsTrue(machine.ExecuteStep(context));   // second PUSH
            Assert.AreEqual(2, context.Stack.Count);
            Assert.IsFalse(machine.ExecuteStep(context));  // EOF
        }

        // ── Null context ──────────────────────────────────────────────────────

        [TestMethod]
        public void Execute_NullContext_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => new TestMachine().Execute(null!));
        }

        [TestMethod]
        public void ExecuteStep_NullContext_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => new TestMachine().ExecuteStep(null!));
        }

        // ── Item 48: InstructionAttribute and RegisterInstruction reject empty/whitespace names ──

        [TestMethod]
        public void InstructionAttribute_EmptyName_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(
                () => new InstructionAttribute("", 0x01));
        }

        [TestMethod]
        public void InstructionAttribute_WhitespaceName_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(
                () => new InstructionAttribute("   ", 0x01));
        }

        [TestMethod]
        public void RegisterInstruction_EmptyName_ThrowsArgumentException()
        {
            var machine = new TestMachine();
            Assert.ThrowsException<ArgumentException>(
                () => machine.RegisterInstruction([0xA0], "", _ => { }));
        }

        [TestMethod]
        public void RegisterInstruction_WhitespaceName_ThrowsArgumentException()
        {
            var machine = new TestMachine();
            Assert.ThrowsException<ArgumentException>(
                () => machine.RegisterInstruction([0xA0], "   ", _ => { }));
        }

        // ── CancellationToken ─────────────────────────────────────────────────

        [TestMethod]
        public void Execute_CancellationToken_StopsExecution()
        {
            byte[] instructions = [0x01, 0x01, 0x10,  0x01, 0x01, 0x01];
            var context = new DefaultContext(instructions);
            TestMachine machine = new TestMachine();

            using var cts = new CancellationTokenSource();
            cts.Cancel();  // already cancelled

            Assert.ThrowsException<OperationCanceledException>(
                () => machine.Execute(context, cts.Token));
        }

        // ── RegisterInstruction ───────────────────────────────────────────────

        [TestMethod]
        public void RegisterInstruction_AddsRuntimeOpcode()
        {
            var machine = new TestMachine();
            bool executed = false;
            machine.RegisterInstruction([0xFF], "NOP", _ => executed = true);

            var context = new DefaultContext(new byte[] { 0xFF });
            machine.Execute(context);
            Assert.IsTrue(executed);
        }

        [TestMethod]
        public void RegisterInstruction_EmptyOpcode_Throws()
        {
            var machine = new TestMachine();
            Assert.ThrowsException<ArgumentException>(
                () => machine.RegisterInstruction([], "NOP", _ => { }));
        }

        // ── AllowMultiple [Instruction] ───────────────────────────────────────

        [TestMethod]
        public void MultipleOpcodes_SameMethod_BothWork()
        {
            // Both 0x01,0x01 and 0x01,0x03 call PushByte.
            byte[] instructions1 = [0x01, 0x01, 0x42];
            byte[] instructions2 = [0x01, 0x03, 0x42];

            var ctx1 = new DefaultContext(instructions1);
            var ctx2 = new DefaultContext(instructions2);
            TestMachine machine = new TestMachine();

            machine.Execute(ctx1);
            machine.Execute(ctx2);

            Assert.AreEqual((byte)0x42, ctx1.Stack.Peek());
            Assert.AreEqual((byte)0x42, ctx2.Stack.Peek());
        }

        // ── Item 3: opcode keys are immutable after registration ──────────────

        [TestMethod]
        public void RegisterInstruction_MutateSourceArrayAfterRegistration_DispatchUnchanged()
        {
            var machine = new TestMachine();
            bool executed = false;
            byte[] opcode = [0xAB];
            machine.RegisterInstruction(opcode, "TEST", _ => executed = true);

            // Mutate the original array — must not affect dispatch.
            opcode[0] = 0x00;

            var context = new DefaultContext(new byte[] { 0xAB });
            machine.Execute(context);
            Assert.IsTrue(executed, "Handler must still dispatch via the original opcode 0xAB.");
        }

        // ── Item 1: prefix-conflicting opcodes are rejected ───────────────────

        [TestMethod]
        public void RegisterInstruction_ShorterPrefixConflict_Throws()
        {
            // [0x10] already registered; [0x10, 0x20] is rejected because [0x10] is a prefix of it.
            var machine = new TestMachine(); // TestMachine has [0x10,0x01] and [0x10,0x02]
            machine.RegisterInstruction([0xAA], "A", _ => { });
            Assert.ThrowsException<ArgumentException>(
                () => machine.RegisterInstruction([0xAA, 0x01], "B", _ => { }));
        }

        [TestMethod]
        public void RegisterInstruction_LongerPrefixConflict_Throws()
        {
            // [0xBB, 0x01] registered first; [0xBB] alone conflicts because it is a prefix.
            var machine = new TestMachine();
            machine.RegisterInstruction([0xBB, 0x01], "LONG", _ => { });
            Assert.ThrowsException<ArgumentException>(
                () => machine.RegisterInstruction([0xBB], "SHORT", _ => { }));
        }

        [TestMethod]
        public void RegisterInstruction_NonPrefixConflict_Succeeds()
        {
            // [0xCC] and [0xDD] share no prefix — both should register without error.
            var machine = new TestMachine();
            machine.RegisterInstruction([0xCC], "A", _ => { });
            machine.RegisterInstruction([0xDD], "B", _ => { }); // must not throw
        }

        // ── Item 1: prefix-conflict rules apply even with overwrite: true ────────

        [TestMethod]
        public void RegisterInstruction_Overwrite_SameOpcode_Succeeds()
        {
            // Replacing an instruction with the same opcode must succeed regardless of overwrite.
            var machine = new TestMachine();
            machine.RegisterInstruction([0xEE], "OLD", _ => { });
            machine.RegisterInstruction([0xEE], "NEW", _ => { }, overwrite: true); // must not throw
        }

        [TestMethod]
        public void RegisterInstruction_Overwrite_ShorterPrefixConflict_Throws()
        {
            // overwrite: true cannot bypass the prefix-conflict rule.
            // [0xAA] registered first; [0xAA, 0x01] would make [0xAA] a prefix — rejected.
            var machine = new TestMachine();
            machine.RegisterInstruction([0xAA], "SHORT", _ => { });
            Assert.ThrowsException<ArgumentException>(
                () => machine.RegisterInstruction([0xAA, 0x01], "LONG", _ => { }, overwrite: true));
        }

        [TestMethod]
        public void RegisterInstruction_Overwrite_LongerPrefixConflict_Throws()
        {
            // [0xBB, 0x01] registered first; [0xBB] alone is a prefix of it — rejected even with overwrite.
            var machine = new TestMachine();
            machine.RegisterInstruction([0xBB, 0x01], "LONG", _ => { });
            Assert.ThrowsException<ArgumentException>(
                () => machine.RegisterInstruction([0xBB], "SHORT", _ => { }, overwrite: true));
        }

        // ── InvertedReader (endianness swap, zero allocation) ─────────────────

        [TestMethod]
        public void BigEndianReader_ReadInt16_ReversesBytes()
        {
            // On a little-endian system, requesting big-endian triggers InvertedReader.
            var reader = NumberReader.GetReader(littleEndian: !BitConverter.IsLittleEndian);
            // Value 0x0102 in big-endian = bytes [0x01, 0x02]
            byte[] data = [0x01, 0x02];
            var ctx = new DefaultContext(data);
            short value = reader.ReadInt16(ctx);
            Assert.AreEqual((short)0x0102, value);
        }

        // ── Error paths ───────────────────────────────────────────────────────

        [TestMethod]
        public void Execute_UnknownOpcode_ThrowsWithDetails()
        {
            var context = new DefaultContext(new byte[] { 0xFF });
            var machine = new TestMachine();
            var ex = Assert.ThrowsException<VirtualProcessorException>(() => machine.Execute(context));
            Assert.AreEqual(0, ex.InstructionPointer);
            CollectionAssert.AreEqual(new byte[] { 0xFF }, ex.OpcodeBytes);
        }

        [TestMethod]
        public void Execute_PartialMultiByteOpcode_ThrowsVirtualProcessorException()
        {
            // 0x01 alone doesn't match any instruction — all opcodes starting with 0x01 are 2 bytes.
            var context = new DefaultContext(new byte[] { 0x01 });
            var machine = new TestMachine();
            var ex = Assert.ThrowsException<VirtualProcessorException>(() => machine.Execute(context));
            Assert.AreEqual(0, ex.InstructionPointer);
        }

        [TestMethod]
        public void Execute_IncompleteOperand_ThrowsVirtualProcessorException()
        {
            // Opcode [0x01, 0x01] (PUSH_BYTE) matches, but no operand byte follows.
            var context = new DefaultContext(new byte[] { 0x01, 0x01 });
            var machine = new TestMachine();
            var ex = Assert.ThrowsException<VirtualProcessorException>(() => machine.Execute(context));
            Assert.AreEqual(0, ex.InstructionPointer);
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x01 }, ex.OpcodeBytes);
            Assert.IsNotNull(ex.InnerException);
        }

        // ── LEB128 ───────────────────────────────────────────────────────────

        [TestMethod]
        public void ReadULEB128_SingleByte_ReturnsValue()
        {
            // 0x42 (66) — single byte, no continuation bit
            byte[] instructions = [0x20, 0x42];
            var context = new DefaultContext(instructions);
            new LEB128TestMachine().Execute(context);
            Assert.AreEqual((ulong)66, context.Stack.Peek());
        }

        [TestMethod]
        public void ReadULEB128_MultipleBytes_ReturnsValue()
        {
            // 300 = [0xAC, 0x02] in ULEB128
            byte[] instructions = [0x20, 0xAC, 0x02];
            var context = new DefaultContext(instructions);
            new LEB128TestMachine().Execute(context);
            Assert.AreEqual((ulong)300, context.Stack.Peek());
        }

        [TestMethod]
        public void ReadULEB128_MaxSingleByte_ReturnsValue()
        {
            // 127 — largest value fitting in one LEB128 byte
            byte[] instructions = [0x20, 0x7F];
            var context = new DefaultContext(instructions);
            new LEB128TestMachine().Execute(context);
            Assert.AreEqual((ulong)127, context.Stack.Peek());
        }

        [TestMethod]
        public void ReadSLEB128_PositiveValue_ReturnsValue()
        {
            // 63 — positive, single byte, sign bit (0x40) not set
            byte[] instructions = [0x21, 0x3F];
            var context = new DefaultContext(instructions);
            new LEB128TestMachine().Execute(context);
            Assert.AreEqual(63L, context.Stack.Peek());
        }

        [TestMethod]
        public void ReadSLEB128_NegativeOne_ReturnsValue()
        {
            // -1 = [0x7F] in SLEB128 (sign bit 0x40 set, sign-extended to all ones)
            byte[] instructions = [0x21, 0x7F];
            var context = new DefaultContext(instructions);
            new LEB128TestMachine().Execute(context);
            Assert.AreEqual(-1L, context.Stack.Peek());
        }

        [TestMethod]
        public void ReadSLEB128_NegativeMultiByte_ReturnsValue()
        {
            // -128 = [0x80, 0x7F] in SLEB128
            byte[] instructions = [0x21, 0x80, 0x7F];
            var context = new DefaultContext(instructions);
            new LEB128TestMachine().Execute(context);
            Assert.AreEqual(-128L, context.Stack.Peek());
        }

        // ── Item 17: LEB128 overlong and overflow rejection ───────────────────

        [TestMethod]
        public void ReadULEB128_ElevenBytes_ThrowsFormatException()
        {
            // 11 bytes all with the continuation bit set — exceeds the 10-byte maximum.
            byte[] payload = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00];
            byte[] instructions = [0x20, .. payload];
            var context = new DefaultContext(instructions);
            Assert.ThrowsException<FormatException>(() => new LEB128TestMachine().Execute(context));
        }

        [TestMethod]
        public void ReadULEB128_TenthByteInvalidHighBits_ThrowsFormatException()
        {
            // 9 continuation bytes followed by a 10th byte that has payload bits beyond bit 63.
            // Valid 10th byte for ULEB128 is 0x00 or 0x01 only.
            byte[] payload = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02];
            byte[] instructions = [0x20, .. payload];
            var context = new DefaultContext(instructions);
            Assert.ThrowsException<FormatException>(() => new LEB128TestMachine().Execute(context));
        }

        [TestMethod]
        public void ReadULEB128_MaxUInt64_DecodesCorrectly()
        {
            // ulong.MaxValue = 0xFFFFFFFFFFFFFFFF in ULEB128:
            // [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01]
            byte[] payload = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01];
            byte[] instructions = [0x20, .. payload];
            var context = new DefaultContext(instructions);
            new LEB128TestMachine().Execute(context);
            Assert.AreEqual(ulong.MaxValue, context.Stack.Peek());
        }

        [TestMethod]
        public void ReadSLEB128_ElevenBytes_ThrowsFormatException()
        {
            byte[] payload = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00];
            byte[] instructions = [0x21, .. payload];
            var context = new DefaultContext(instructions);
            Assert.ThrowsException<FormatException>(() => new LEB128TestMachine().Execute(context));
        }

        [TestMethod]
        public void ReadSLEB128_TenthByteZeroHighBits_ThrowsFormatException()
        {
            // 0x01: bit63=1 (negative value component) but bits 1-6=0, inconsistent with
            // negative sign extension (bits 1-6 should be all-ones for a valid negative).
            // The existing test only checked 11-byte sequences; this covers the 10th-byte rule.
            byte[] payload = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01];
            byte[] instructions = [0x21, .. payload];
            var context = new DefaultContext(instructions);
            Assert.ThrowsException<FormatException>(() => new LEB128TestMachine().Execute(context));
        }

        [TestMethod]
        public void ReadSLEB128_TenthBytePartialSignExtension_ThrowsFormatException()
        {
            // 0x7E: bits 1-6 are all-one (suggesting negative) but bit 0=0 (bit63 not set),
            // which is inconsistent with positive sign extension (bits 1-6 should all be zero).
            byte[] payload = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x7E];
            byte[] instructions = [0x21, .. payload];
            var context = new DefaultContext(instructions);
            Assert.ThrowsException<FormatException>(() => new LEB128TestMachine().Execute(context));
        }

        [TestMethod]
        public void ReadSLEB128_MaxInt64_DecodesCorrectly()
        {
            // long.MaxValue = 0x7FFFFFFFFFFFFFFF in SLEB128:
            // [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00]
            byte[] payload = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
            byte[] instructions = [0x21, .. payload];
            var context = new DefaultContext(instructions);
            new LEB128TestMachine().Execute(context);
            Assert.AreEqual(long.MaxValue, context.Stack.Peek());
        }

        [TestMethod]
        public void ReadSLEB128_MinInt64_DecodesCorrectly()
        {
            // long.MinValue = -9223372036854775808 in SLEB128:
            // [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x7F]
            byte[] payload = [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x7F];
            byte[] instructions = [0x21, .. payload];
            var context = new DefaultContext(instructions);
            new LEB128TestMachine().Execute(context);
            Assert.AreEqual(long.MinValue, context.Stack.Peek());
        }

        [TestMethod]
        public void RegisterInstruction_DuplicateOpcode_Throws()
        {
            var machine = new TestMachine();
            machine.RegisterInstruction([0xFF], "NOP1", _ => { });
            Assert.ThrowsException<ArgumentException>(
                () => machine.RegisterInstruction([0xFF], "NOP2", _ => { }));
        }

        [TestMethod]
        public void Instructions_IncludesAttributeAndRuntimeRegistered()
        {
            var machine = new TestMachine();
            machine.RegisterInstruction([0xFF], "NOP", _ => { });
            var names = machine.Instructions.Select(i => i.Name).ToHashSet();
            Assert.IsTrue(names.Contains("PUSH_BYTE"));
            Assert.IsTrue(names.Contains("POP"));
            Assert.IsTrue(names.Contains("NOP"));
        }

        // ── OnStep ────────────────────────────────────────────────────────────

        [TestMethod]
        public void OnStep_IsCalledBeforeEachInstruction()
        {
            // Two PUSH instructions → OnStep must fire twice, each time with the correct IP.
            byte[] instructions = [0x01, 0x01, 0x10,  0x01, 0x01, 0x01];
            var context = new DefaultContext(instructions);

            var recordedPointers = new System.Collections.Generic.List<int>();
            var machine = new TrackingTestMachine(recordedPointers);
            machine.Execute(context);

            Assert.AreEqual(2, recordedPointers.Count);
            Assert.AreEqual(0, recordedPointers[0]); // first PUSH starts at 0
            Assert.AreEqual(3, recordedPointers[1]); // second PUSH starts at 3
        }

        [TestMethod]
        public void OnStep_IsCalledByExecuteStep()
        {
            byte[] instructions = [0x01, 0x01, 0x42];
            var context = new DefaultContext(instructions);

            var recordedPointers = new System.Collections.Generic.List<int>();
            var machine = new TrackingTestMachine(recordedPointers);
            machine.ExecuteStep(context);

            Assert.AreEqual(1, recordedPointers.Count);
            Assert.AreEqual(0, recordedPointers[0]);
        }

        // ── IP = -1 termination ───────────────────────────────────────────────

        [TestMethod]
        public void Execute_NegativeInstructionPointer_TerminatesImmediately()
        {
            // Start with IP = -1: the Execute loop must not dispatch any instruction.
            byte[] instructions = [0x02]; // POP — would crash on empty stack
            var context = new DefaultContext(instructions);
            context.InstructionPointer = -1;

            new TestMachine().Execute(context); // must not throw
            Assert.AreEqual(0, context.Stack.Count);
        }

        // ── InstructionAttribute validation ───────────────────────────────────

        [TestMethod]
        public void InstructionAttribute_NullName_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new InstructionAttribute(null!, 0x01));
        }

        [TestMethod]
        public void InstructionAttribute_NullInstruction_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new InstructionAttribute("NOP", null!));
        }

        [TestMethod]
        public void InstructionAttribute_EmptyOpcode_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => new InstructionAttribute("NOP"));
        }

        [TestMethod]
        public void InstructionAttribute_ValidArgs_StoresNameAndInstruction()
        {
            var attr = new InstructionAttribute("HALT", 0xFF);
            Assert.AreEqual("HALT", attr.Name);
            CollectionAssert.AreEqual(new byte[] { 0xFF }, attr.Instruction);
        }

        // ── Parameterless instruction (NOOP) ──────────────────────────────────────

        [TestMethod]
        public void ParameterlessInstruction_IsDispatched()
        {
            var machine = new NoopTestMachine();
            var ctx = new DefaultContext(new byte[] { 0xF0 });
            machine.Execute(ctx);
            Assert.IsTrue(machine.NopExecuted);
        }

        [TestMethod]
        public void ParameterlessInstruction_DoesNotConsumeOperandBytes()
        {
            // NOP (0xF0) followed by POP (0x02) — POP must execute correctly after NOP.
            byte[] program = [0xF0, 0x02];
            var machine = new NoopTestMachine();
            // Inherit POP from TestMachine? No — NoopTestMachine has no POP.
            // Just verify NOP + EOF leaves the IP at position 1 (NOP consumes only its opcode).
            var ctx = new DefaultContext(program);
            machine.ExecuteStep(ctx); // NOP
            Assert.AreEqual(1, ctx.InstructionPointer);
            Assert.IsTrue(machine.NopExecuted);
        }

        // ── ReadSByte ─────────────────────────────────────────────────────────

        [TestMethod]
        public void ReadSByte_PositiveValue_PushedCorrectly()
        {
            byte[] instructions = [0x30, 0x7F]; // PUSH_SBYTE 127
            var context = new DefaultContext(instructions);
            new SByteTestMachine().Execute(context);
            Assert.AreEqual((sbyte)127, (sbyte)context.Stack.Peek());
        }

        [TestMethod]
        public void ReadSByte_NegativeValue_SignExtended()
        {
            byte[] instructions = [0x30, 0xFF]; // PUSH_SBYTE -1
            var context = new DefaultContext(instructions);
            new SByteTestMachine().Execute(context);
            Assert.AreEqual((sbyte)-1, (sbyte)context.Stack.Peek());
        }

        // ── Item 18: failed byte read must not advance IP ─────────────────────

        [TestMethod]
        public void ReadByte_FailedRead_InstructionPointerRestoredToInstructionStart()
        {
            // Item 19: a failed operand read must restore InstructionPointer to the start of
            // the failing instruction so that callers can identify or retry it.
            // SByteTestMachine (PUSH_SBYTE 0x30) reads the opcode byte, then tries to read
            // the operand sbyte — which is missing. The handler threw, so IP must be 0.
            byte[] instructions = [0x30]; // PUSH_SBYTE opcode with no operand
            var context = new DefaultContext(instructions);

            Assert.ThrowsException<VirtualProcessorException>(
                () => new SByteTestMachine().Execute(context));

            // IP must be restored to the instruction start (0), not left at 1 (after opcode).
            Assert.AreEqual(0, context.InstructionPointer);
        }

        [TestMethod]
        public void FailedSecondOperand_InstructionPointerRestoredToInstructionStart()
        {
            // Item 19: when the second of two operands is truncated, the first operand was already
            // read (advancing IP). IP must still be restored to the instruction's start address.
            // IntMachine's PUSH_INT (0x40) reads one int (4 bytes). We provide only 3 bytes
            // after the opcode so the read fails mid-operand.
            byte[] instructions = [0x40, 0x01, 0x02, 0x03]; // PUSH_INT with 3 of 4 operand bytes
            var ctx = new TypedStackContext<int>(instructions);

            Assert.ThrowsException<VirtualProcessorException>(
                () => new IntMachine().Execute(ctx));

            // IP must point to the instruction start (0), not to after the partial operand.
            Assert.AreEqual(0, ctx.InstructionPointer);
        }

        [TestMethod]
        public void ReadSByte_BigEndian_SameResultAsLittleEndian()
        {
            // sbyte is one byte — endianness has no effect
            var readerLE = NumberReader.GetReader(littleEndian: true);
            var readerBE = NumberReader.GetReader(littleEndian: false);
            byte[] data = [0x80]; // -128 as sbyte
            Assert.AreEqual(readerLE.ReadSByte(new DefaultContext(data)),
                            readerBE.ReadSByte(new DefaultContext(data)));
        }

        // ── TypedStackContext<TValue> ─────────────────────────────────────────

        [TestMethod]
        public void TypedStackContext_PushAndAdd_ReturnsCorrectSum()
        {
            byte[] prog = [
                0x40, 0x0A, 0, 0, 0,  // PUSH_INT 10
                0x40, 0x05, 0, 0, 0,  // PUSH_INT 5
                0x41                   // ADD_INT
            ];
            var ctx = new TypedStackContext<int>(prog);
            new IntMachine().Execute(ctx);
            Assert.AreEqual(15, ctx.Stack.Peek());
        }

        // ── RegisterInstruction with overwrite ───────────────────────────────

        [TestMethod]
        public void RegisterInstruction_Overwrite_ReplacesHandler()
        {
            var machine = new TestMachine();
            machine.RegisterInstruction([0xFF], "OLD", _ => { });
            int newCount = 0;
            machine.RegisterInstruction([0xFF], "NEW", _ => newCount++, overwrite: true);

            var context = new DefaultContext(new byte[] { 0xFF });
            machine.Execute(context);
            Assert.AreEqual(1, newCount);
        }

        [TestMethod]
        public void RegisterInstruction_Overwrite_UpdatesName()
        {
            var machine = new TestMachine();
            machine.RegisterInstruction([0xFF], "OLD", _ => { });
            machine.RegisterInstruction([0xFF], "NEW", _ => { }, overwrite: true);
            Assert.IsTrue(machine.Instructions.Any(i => i.Name == "NEW"));
            Assert.IsFalse(machine.Instructions.Any(i => i.Name == "OLD"));
        }

        // ── Item 38: OpcodeBytes returns a defensive copy ─────────────────────

        [TestMethod]
        public void VirtualProcessorException_OpcodeBytes_MutationDoesNotAlterDiagnostics()
        {
            var context = new DefaultContext(new byte[] { 0xFF });
            var ex = Assert.ThrowsException<VirtualProcessorException>(
                () => new TestMachine().Execute(context));

            byte[]? first = ex.OpcodeBytes;
            Assert.IsNotNull(first);
            first![0] = 0x00; // mutate the returned copy

            byte[]? second = ex.OpcodeBytes;
            Assert.AreEqual(0xFF, second![0], "Mutation of a returned OpcodeBytes array must not affect subsequent accesses.");
        }

        // ── InstructionName in VirtualProcessorException ──────────────────────

        [TestMethod]
        public void Execute_IncompleteOperand_ExceptionContainsInstructionName()
        {
            // PUSH_BYTE matches [0x01, 0x01] but operand byte is missing
            var context = new DefaultContext(new byte[] { 0x01, 0x01 });
            var ex = Assert.ThrowsException<VirtualProcessorException>(
                () => new TestMachine().Execute(context));
            Assert.AreEqual("PUSH_BYTE", ex.InstructionName);
        }

        [TestMethod]
        public void Execute_UnknownOpcode_InstructionNameIsNull()
        {
            // Unknown opcode: no instruction matched, so no name is available
            var context = new DefaultContext(new byte[] { 0xAA });
            var ex = Assert.ThrowsException<VirtualProcessorException>(
                () => new TestMachine().Execute(context));
            Assert.IsNull(ex.InstructionName);
        }

        // ── Context.Data as ReadOnlyMemory<byte> ─────────────────────────────

        [TestMethod]
        public void Context_Data_AcceptsMemorySlice()
        {
            // Execute only a slice of a larger byte array without copying
            byte[] backing = [0x00, 0x01, 0x01, 0x42, 0x00]; // [padding, PUSH_BYTE, operand, padding]
            var memory = new ReadOnlyMemory<byte>(backing, 1, 3);
            var context = new DefaultContext(memory);
            new TestMachine().Execute(context);
            Assert.AreEqual((byte)0x42, context.Stack.Peek());
        }

        // ── IVmInspector / Breakpoints ────────────────────────────────────────

        private sealed class RecordingInspector : IVmInspector<DefaultContext>
        {
            public readonly List<(int Address, string Name)> Instructions = [];
            public readonly List<(int Address, string Name)> HitBreakpoints = [];

            public void BeforeInstruction(DefaultContext ctx, int address, string instructionName)
                => Instructions.Add((address, instructionName));

            public void OnBreakpoint(DefaultContext ctx, int address, string instructionName)
                => HitBreakpoints.Add((address, instructionName));
        }

        private sealed class DelegateInspector<T> : IVmInspector<T> where T : Context
        {
            private readonly Action<T, int, string> _beforeInstruction;
            private readonly Action<T, int, string> _onBreakpoint;

            internal DelegateInspector(Action<T, int, string> beforeInstruction, Action<T, int, string> onBreakpoint)
            {
                _beforeInstruction = beforeInstruction;
                _onBreakpoint = onBreakpoint;
            }

            public void BeforeInstruction(T ctx, int address, string name) => _beforeInstruction(ctx, address, name);
            public void OnBreakpoint(T ctx, int address, string name) => _onBreakpoint(ctx, address, name);
        }

        [TestMethod]
        public void Inspector_BeforeInstruction_CalledForEachInstruction()
        {
            var machine = new InspectorMachine();
            var inspector = new RecordingInspector();
            machine.Inspector = inspector;

            // PUSH_A (addr 0), PUSH_B (addr 1), ADD (addr 2)
            byte[] program = [0x01, 0x02, 0x10];
            machine.Execute(new DefaultContext(program));

            Assert.AreEqual(3, inspector.Instructions.Count);
            Assert.AreEqual("PUSH_A", inspector.Instructions[0].Name);
            Assert.AreEqual("PUSH_B", inspector.Instructions[1].Name);
            Assert.AreEqual("ADD",    inspector.Instructions[2].Name);
            Assert.AreEqual(0, inspector.Instructions[0].Address);
            Assert.AreEqual(1, inspector.Instructions[1].Address);
            Assert.AreEqual(2, inspector.Instructions[2].Address);
        }

        [TestMethod]
        public void Inspector_OnBreakpoint_CalledOnlyAtBreakpointAddress()
        {
            var machine = new InspectorMachine();
            var inspector = new RecordingInspector();
            machine.Inspector = inspector;
            machine.Breakpoints.Add(1); // break at PUSH_B

            byte[] program = [0x01, 0x02, 0x10];
            machine.Execute(new DefaultContext(program));

            Assert.AreEqual(1, inspector.HitBreakpoints.Count);
            Assert.AreEqual(1,        inspector.HitBreakpoints[0].Address);
            Assert.AreEqual("PUSH_B", inspector.HitBreakpoints[0].Name);
            Assert.AreEqual(3, inspector.Instructions.Count); // BeforeInstruction fires for all 3
        }

        [TestMethod]
        public void Inspector_OnBreakpoint_FiresBeforeBeforeInstruction()
        {
            var machine = new InspectorMachine();
            var order = new List<string>();
            machine.Inspector = new DelegateInspector<DefaultContext>(
                beforeInstruction: (ctx, addr, name) => order.Add($"before:{addr}"),
                onBreakpoint:      (ctx, addr, name) => order.Add($"break:{addr}")
            );
            machine.Breakpoints.Add(0);

            byte[] program = [0x01]; // single PUSH_A
            machine.Execute(new DefaultContext(program));

            Assert.AreEqual(2, order.Count);
            Assert.AreEqual("break:0",  order[0]);
            Assert.AreEqual("before:0", order[1]);
        }

        [TestMethod]
        public void Inspector_Null_ExecutesNormally()
        {
            var machine = new InspectorMachine();
            machine.Inspector = null;

            byte[] program = [0x01, 0x02, 0x10];
            var ctx = new DefaultContext(program);
            machine.Execute(ctx);

            Assert.AreEqual(8, (int)ctx.Stack.Peek());
        }

        [TestMethod]
        public void Breakpoints_WithoutInspector_DoNotAffectExecution()
        {
            var machine = new InspectorMachine();
            machine.Inspector = null;
            machine.Breakpoints.Add(0);

            byte[] program = [0x01, 0x02, 0x10];
            var ctx = new DefaultContext(program);
            machine.Execute(ctx);

            Assert.AreEqual(8, (int)ctx.Stack.Peek());
        }

        // ── Fast dispatch path ────────────────────────────────────────────────

        [TestMethod]
        public void FastDispatch_SingleByteOpcode_ExecutesCorrectly()
        {
            // InspectorMachine has only single-byte opcodes with no multi-byte peers.
            var machine = new InspectorMachine();
            byte[] program = [0x01, 0x02, 0x10]; // PUSH_A(5), PUSH_B(3), ADD → 8
            var ctx = new DefaultContext(program);
            machine.Execute(ctx);
            Assert.AreEqual(8, (int)ctx.Stack.Peek());
        }

        [TestMethod]
        public void FastDispatch_MultiByteOpcode_ExcludedFromFastPath_StillExecutes()
        {
            // TrackingTestMachine has PUSH_BYTE=[0x01,0x01] (multi-byte starting with 0x01).
            // Byte 0x01 must NOT use the fast path; the slow path must handle it correctly.
            var machine = new TrackingTestMachine(new List<int>());
            byte[] program = [0x01, 0x01, 42]; // PUSH_BYTE 42
            var ctx = new DefaultContext(program);
            machine.Execute(ctx);
            Assert.AreEqual(42, (byte)ctx.Stack.Peek());
        }

        [TestMethod]
        public void FastDispatch_NoopMachine_FastPathHitsNop()
        {
            // NoopTestMachine has NOP=0xF0 only — unambiguous single-byte, uses fast path.
            var machine = new NoopTestMachine();
            byte[] program = [0xF0, 0xF0]; // two NOPs
            machine.Execute(new DefaultContext(program));
            Assert.IsTrue(machine.NopExecuted);
        }

        // ── Item 31: OnBreakpoint redirect suppresses BeforeInstruction ──────

        [TestMethod]
        public void Inspector_OnBreakpoint_MultiByteOpcodeWithoutRedirect_BeforeInstructionCalled()
        {
            // Regression guard: a breakpoint on a multi-byte opcode (slow dispatch path) must
            // still call BeforeInstruction when OnBreakpoint does not redirect execution.
            // Before the fix, NotifyInspector compared IP against instructionStart, but the slow
            // path already advances IP past the opcode bytes before the call — so the comparison
            // was always true and BeforeInstruction was silently skipped.
            var machine = new TestMachine(); // multi-byte opcodes only (slow path)
            var beforeInstructionCalled = false;
            machine.Inspector = new DelegateInspector<DefaultContext>(
                beforeInstruction: (ctx, addr, name) => beforeInstructionCalled = true,
                onBreakpoint: (ctx, addr, name) => { } // no redirect
            );
            machine.Breakpoints.Add(0);

            // PUSH_BYTE ([0x01, 0x01] + operand 0x42) — multi-byte opcode at address 0.
            byte[] program = [0x01, 0x01, 0x42];
            machine.Execute(new DefaultContext(program));

            Assert.IsTrue(beforeInstructionCalled,
                "BeforeInstruction must be called when OnBreakpoint does not redirect, even for a multi-byte opcode.");
        }

        [TestMethod]
        public void Inspector_OnBreakpoint_Redirect_BeforeInstructionNotCalled()
        {
            // When OnBreakpoint changes InstructionPointer, BeforeInstruction must not be called
            // with stale metadata for the old instruction.
            var machine = new InspectorMachine();
            var beforeInstructionCalled = false;
            machine.Inspector = new DelegateInspector<DefaultContext>(
                beforeInstruction: (ctx, addr, name) => beforeInstructionCalled = true,
                onBreakpoint: (ctx, addr, name) => ctx.InstructionPointer = ctx.Data.Length // redirect
            );
            machine.Breakpoints.Add(0);

            byte[] program = [0x01]; // single PUSH_A
            machine.Execute(new DefaultContext(program));

            Assert.IsFalse(beforeInstructionCalled,
                "BeforeInstruction must not be called when OnBreakpoint has already redirected execution.");
        }

        [TestMethod]
        public void Inspector_RedirectIpInBeforeInstruction_SkipsDispatch()
        {
            // If BeforeInstruction changes IP, the instruction must not be dispatched.
            var machine = new InspectorMachine();
            machine.Inspector = new DelegateInspector<DefaultContext>(
                beforeInstruction: (ctx, addr, name) => ctx.InstructionPointer = ctx.Data.Length,
                onBreakpoint: (ctx, addr, name) => { }
            );

            byte[] program = [0x01]; // PUSH_A would push 5 — must not run
            var ctx = new DefaultContext(program);
            machine.Execute(ctx);

            Assert.AreEqual(0, ctx.Stack.Count);
        }

        [TestMethod]
        public void Inspector_RedirectIpInOnBreakpoint_SkipsDispatch()
        {
            // If OnBreakpoint changes IP, the instruction must not be dispatched.
            var machine = new InspectorMachine();
            machine.Breakpoints.Add(0);
            machine.Inspector = new DelegateInspector<DefaultContext>(
                beforeInstruction: (ctx, addr, name) => { },
                onBreakpoint: (ctx, addr, name) => ctx.InstructionPointer = ctx.Data.Length
            );

            byte[] program = [0x01]; // PUSH_A would push 5 — must not run
            var ctx = new DefaultContext(program);
            machine.Execute(ctx);

            Assert.AreEqual(0, ctx.Stack.Count);
        }
    }

    // ── BoundedStack + operand-stack depth limit (item 27) ───────────────────────────────────

    [TestClass]
    public class BoundedStackTests
    {
        [TestMethod]
        public void BoundedStack_Push_BeyondMaxDepth_ThrowsInvalidOperationException()
        {
            var stack = new BoundedStack<int>(maxDepth: 2);
            stack.Push(1);
            stack.Push(2);
            Assert.ThrowsException<InvalidOperationException>(() => stack.Push(3));
        }

        [TestMethod]
        public void BoundedStack_Pop_EmptyStack_ThrowsInvalidOperationException()
        {
            var stack = new BoundedStack<int>();
            Assert.ThrowsException<InvalidOperationException>(() => stack.Pop());
        }

        [TestMethod]
        public void BoundedStack_Peek_EmptyStack_ThrowsInvalidOperationException()
        {
            var stack = new BoundedStack<int>();
            Assert.ThrowsException<InvalidOperationException>(() => stack.Peek());
        }

        [TestMethod]
        public void BoundedStack_InvalidMaxDepth_ThrowsArgumentOutOfRangeException()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoundedStack<int>(0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BoundedStack<int>(-1));
        }

        [TestMethod]
        public void BoundedStack_DefaultMaxDepth_Is1024()
        {
            var stack = new BoundedStack<object>();
            Assert.AreEqual(1024, stack.MaxDepth);
            Assert.AreEqual(BoundedStack<object>.DefaultMaxDepth, stack.MaxDepth);
        }

        [TestMethod]
        public void DefaultContext_Stack_IsBoundedStack()
        {
            var ctx = new DefaultContext(ReadOnlyMemory<byte>.Empty);
            Assert.IsInstanceOfType<BoundedStack<object>>(ctx.Stack);
        }

        [TestMethod]
        public void TypedStackContext_Stack_IsBoundedStack()
        {
            var ctx = new TypedStackContext<int>(ReadOnlyMemory<byte>.Empty);
            Assert.IsInstanceOfType<BoundedStack<int>>(ctx.Stack);
        }

        [TestMethod]
        public void DefaultContext_CustomMaxDepth_Enforced()
        {
            var ctx = new DefaultContext(ReadOnlyMemory<byte>.Empty, maxOperandStackDepth: 1);
            ctx.Stack.Push(42);
            Assert.ThrowsException<InvalidOperationException>(() => ctx.Stack.Push(99));
        }

        [TestMethod]
        public void TypedStackContext_CustomMaxDepth_Enforced()
        {
            var ctx = new TypedStackContext<int>(ReadOnlyMemory<byte>.Empty, maxOperandStackDepth: 1);
            ctx.Stack.Push(1);
            Assert.ThrowsException<InvalidOperationException>(() => ctx.Stack.Push(2));
        }

        // ── Context.Data defensive copy (item 43) ─────────────────────────────────────────────

        [TestMethod]
        public void Context_Data_IsDefensiveCopy_MutatingOriginalArrayDoesNotAffectData()
        {
            byte[] original = [0x01, 0x02, 0x03];
            var ctx = new DefaultContext(original);
            original[0] = 0xFF;
            Assert.AreEqual(0x01, ctx.Data.Span[0]);
        }
    }

    // ── Inspector exceptions (item 32) ────────────────────────────────────────────────────────────

    [TestClass]
    public class InspectorExceptionTests
    {
        private sealed class CallbackInspector<T> : IVmInspector<T> where T : Context
        {
            private readonly Action<T, int, string> _before;
            private readonly Action<T, int, string> _onBp;
            internal CallbackInspector(Action<T, int, string> before, Action<T, int, string> onBp)
            { _before = before; _onBp = onBp; }
            public void BeforeInstruction(T ctx, int addr, string name) => _before(ctx, addr, name);
            public void OnBreakpoint(T ctx, int addr, string name) => _onBp(ctx, addr, name);
        }

        private static CallbackInspector<DefaultContext> MakeInspector(
            Action<DefaultContext, int, string>? before = null,
            Action<DefaultContext, int, string>? onBp = null)
            => new(
                before ?? ((_, _, _) => { }),
                onBp ?? ((_, _, _) => { }));

        [TestMethod]
        public void Inspector_BeforeInstruction_Throws_WrappedAsVmInspectorException()
        {
            var machine = new InspectorMachine();
            machine.Inspector = MakeInspector(
                before: (ctx, addr, name) => throw new InvalidOperationException("boom"));
            byte[] program = [0x01]; // PUSH_A
            var ctx = new DefaultContext(program);
            var ex = Assert.ThrowsException<VmInspectorException>(() => machine.Execute(ctx));
            Assert.AreEqual(VmInspectorPhase.BeforeInstruction, ex.Phase);
            Assert.AreEqual(0, ex.Address);
            Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException);
        }

        [TestMethod]
        public void Inspector_OnBreakpoint_Throws_WrappedAsVmInspectorException()
        {
            var machine = new InspectorMachine();
            machine.Inspector = MakeInspector(
                onBp: (ctx, addr, name) => throw new InvalidOperationException("boom"));
            machine.Breakpoints.Add(0);
            byte[] program = [0x01]; // PUSH_A
            var ctx = new DefaultContext(program);
            var ex = Assert.ThrowsException<VmInspectorException>(() => machine.Execute(ctx));
            Assert.AreEqual(VmInspectorPhase.OnBreakpoint, ex.Phase);
            Assert.AreEqual(0, ex.Address);
            Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException);
        }

        [TestMethod]
        public void Inspector_Exception_CarriesInstructionName()
        {
            var machine = new InspectorMachine();
            machine.Inspector = MakeInspector(
                before: (ctx, addr, name) => throw new InvalidOperationException("boom"));
            byte[] program = [0x01]; // PUSH_A
            var ex = Assert.ThrowsException<VmInspectorException>(
                () => machine.Execute(new DefaultContext(program)));
            Assert.AreEqual("PUSH_A", ex.InstructionName);
        }

        [TestMethod]
        public void Inspector_VmInspectorException_NotRewrapped()
        {
            // VmInspectorException thrown by a callback must propagate unchanged, not double-wrapped.
            var inner = new VmInspectorException("original");
            var machine = new InspectorMachine();
            machine.Inspector = MakeInspector(
                before: (ctx, addr, name) => throw inner);
            byte[] program = [0x01];
            var ex = Assert.ThrowsException<VmInspectorException>(
                () => machine.Execute(new DefaultContext(program)));
            Assert.AreSame(inner, ex);
        }

        [TestMethod]
        public void Inspector_SlowPath_BeforeInstruction_Throws_WrappedAsVmInspectorException()
        {
            // Regression: the slow dispatch path must also wrap inspector exceptions.
            var machine = new TestMachine(); // multi-byte opcodes only (slow path)
            machine.Inspector = MakeInspector(
                before: (ctx, addr, name) => throw new InvalidOperationException("slow"));
            byte[] program = [0x01, 0x01, 42]; // PUSH_BYTE 42 (slow path)
            var ex = Assert.ThrowsException<VmInspectorException>(
                () => machine.Execute(new DefaultContext(program)));
            Assert.AreEqual(VmInspectorPhase.BeforeInstruction, ex.Phase);
        }
    }

    // ── Execute instruction budget (item 21) ──────────────────────────────────────────────────────

    [TestClass]
    public class ExecuteBudgetTests
    {
        [TestMethod]
        public void Execute_WithBudget_ThrowsAfterMaxInstructions()
        {
            var machine = new InspectorMachine();
            machine.RegisterInstruction([0xFF], "LOOP", ctx => ctx.InstructionPointer = 0);
            byte[] program = [0x01, 0xFF]; // PUSH_A, LOOP — infinite loop
            var ctx = new DefaultContext(program);
            Assert.ThrowsException<InstructionBudgetExceededException>(
                () => machine.Execute(ctx, maxInstructions: 5));
        }

        [TestMethod]
        public void Execute_Budget_ExceptionCarriesBudgetValue()
        {
            var machine = new InspectorMachine();
            machine.RegisterInstruction([0xFF], "LOOP", ctx => ctx.InstructionPointer = 0);
            byte[] program = [0x01, 0xFF];
            var ctx = new DefaultContext(program);
            var ex = Assert.ThrowsException<InstructionBudgetExceededException>(
                () => machine.Execute(ctx, maxInstructions: 3));
            Assert.AreEqual(3L, ex.Budget);
        }

        [TestMethod]
        public void Execute_WithBudgetZero_Unlimited_TerminatesNormally()
        {
            var machine = new InspectorMachine();
            byte[] program = [0x01, 0x02, 0x10]; // PUSH_A, PUSH_B, ADD
            var ctx = new DefaultContext(program);
            machine.Execute(ctx, maxInstructions: 0);
            Assert.AreEqual(8, (int)ctx.Stack.Peek());
        }

        [TestMethod]
        public void Execute_WithExactBudget_TerminatesNormally()
        {
            var machine = new InspectorMachine();
            byte[] program = [0x01, 0x02, 0x10]; // PUSH_A, PUSH_B, ADD
            var ctx = new DefaultContext(program);
            machine.Execute(ctx, maxInstructions: 3);
            Assert.AreEqual(8, (int)ctx.Stack.Peek());
        }
    }

    // ── Item 5: operand truncation vs handler exceptions ─────────────────────────────────────────

    [TestClass]
    public class OperandTruncationTests
    {
        /// <summary>
        /// Fast-path (single-byte opcodes): handlers that throw IOOB/AOOB from their own domain
        /// logic; and one instruction where the expression-tree wrapper reads a truncated operand.
        /// </summary>
        private sealed class FastPathFaultMachine : VirtualProcessor<DefaultContext>
        {
            [Instruction("FAULT_IOOB", 0xA0)]
            void FaultIndexOutOfRange(DefaultContext ctx)
            {
                int[] arr = [1, 2, 3];
                _ = arr[10]; // IndexOutOfRangeException from handler domain logic, not bytecode reader
            }

            [Instruction("FAULT_AOOB", 0xA1)]
            void FaultArgumentOutOfRange(DefaultContext ctx)
                => throw new ArgumentOutOfRangeException("x", "handler bug"); // not a truncated operand

            [Instruction("TRUNCATED_BYTE", 0xA2)]
            void TruncatedByte(DefaultContext ctx, byte operand)
            {
                // Expression-tree calls ReadByte before this body; with no operand byte, it truncates.
            }
        }

        /// <summary>
        /// Slow-path (multi-byte opcode): handler that throws IOOB from its own domain logic.
        /// </summary>
        private sealed class SlowPathFaultMachine : VirtualProcessor<DefaultContext>
        {
            [Instruction("FAULT_MULTI_IOOB", 0xB0, 0x01)]
            void FaultMultiByte(DefaultContext ctx)
            {
                int[] arr = [1, 2, 3];
                _ = arr[10]; // IOOB from handler domain logic, slow dispatch path
            }
        }

        [TestMethod]
        public void FastPath_Handler_IndexOutOfRangeException_PropagatesUnchanged()
        {
            // A handler that throws IOOB from its own logic must NOT be wrapped as VirtualProcessorException.
            var machine = new FastPathFaultMachine();
            byte[] program = [0xA0];
            Assert.ThrowsException<IndexOutOfRangeException>(
                () => machine.Execute(new DefaultContext(program)));
        }

        [TestMethod]
        public void FastPath_Handler_ArgumentOutOfRangeException_PropagatesUnchanged()
        {
            // A handler that throws AOOB from its own logic must NOT be wrapped as VirtualProcessorException.
            var machine = new FastPathFaultMachine();
            byte[] program = [0xA1];
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => machine.Execute(new DefaultContext(program)));
        }

        [TestMethod]
        public void FastPath_TruncatedOperand_WrappedAsVirtualProcessorException()
        {
            // Genuine end-of-stream during operand read must be wrapped as VirtualProcessorException.
            var machine = new FastPathFaultMachine();
            byte[] program = [0xA2]; // opcode only, no operand byte
            var ex = Assert.ThrowsException<VirtualProcessorException>(
                () => machine.Execute(new DefaultContext(program)));
            Assert.AreEqual(0, ex.InstructionPointer);
            Assert.AreEqual("TRUNCATED_BYTE", ex.InstructionName);
        }

        [TestMethod]
        public void SlowPath_Handler_IndexOutOfRangeException_PropagatesUnchanged()
        {
            // Same guarantee for the slow dispatch path (multi-byte opcode).
            var machine = new SlowPathFaultMachine();
            byte[] program = [0xB0, 0x01]; // opcode only, no operands
            Assert.ThrowsException<IndexOutOfRangeException>(
                () => machine.Execute(new DefaultContext(program)));
        }
    }

    // ── Item 6: inspector IP timing consistency ───────────────────────────────────────────────

    [TestClass]
    public class InspectorIpTimingTests
    {
        private sealed class RecordingIpInspector : IVmInspector<DefaultContext>
        {
            public readonly List<int> SeenIps = [];
            public void BeforeInstruction(DefaultContext ctx, int addr, string name)
                => SeenIps.Add(ctx.InstructionPointer);
            public void OnBreakpoint(DefaultContext ctx, int addr, string name) { }
        }

        private sealed class ActionInspector : IVmInspector<DefaultContext>
        {
            private readonly Action<DefaultContext, int, string> _before;
            private readonly Action<DefaultContext, int, string> _onBp;
            internal ActionInspector(Action<DefaultContext, int, string> before, Action<DefaultContext, int, string> onBp)
            { _before = before; _onBp = onBp; }
            public void BeforeInstruction(DefaultContext ctx, int addr, string name) => _before(ctx, addr, name);
            public void OnBreakpoint(DefaultContext ctx, int addr, string name) => _onBp(ctx, addr, name);
        }

        [TestMethod]
        public void SlowPath_Inspector_SeesIpAtInstructionStart()
        {
            // Multi-byte opcode (slow dispatch path): IP must equal instructionStart, not afterOpcodeIp.
            var machine = new TestMachine();
            var inspector = new RecordingIpInspector();
            machine.Inspector = inspector;
            // PUSH_BYTE opcode [0x01, 0x01] at address 0 + operand 0x42 — 3 bytes total.
            // Without the fix, inspector would see IP = 2 (after opcode bytes); with the fix: 0.
            byte[] program = [0x01, 0x01, 0x42];
            machine.Execute(new DefaultContext(program));
            Assert.AreEqual(1, inspector.SeenIps.Count);
            Assert.AreEqual(0, inspector.SeenIps[0],
                "Inspector must see IP = instructionStart (0), not the post-opcode position (2).");
        }

        [TestMethod]
        public void FastAndSlowPath_InspectorSees_SameIpPhase()
        {
            // Both dispatch paths must expose IP = instructionStart to the inspector.
            var fastMachine = new InspectorMachine(); // single-byte opcodes → fast path
            var fastInspector = new RecordingIpInspector();
            fastMachine.Inspector = fastInspector;
            fastMachine.Execute(new DefaultContext(new byte[] { 0x01 })); // PUSH_A at address 0

            var slowMachine = new TestMachine(); // multi-byte opcodes → slow path
            var slowInspector = new RecordingIpInspector();
            slowMachine.Inspector = slowInspector;
            slowMachine.Execute(new DefaultContext(new byte[] { 0x01, 0x01, 0x42 })); // PUSH_BYTE at address 0

            Assert.AreEqual(0, fastInspector.SeenIps[0]);
            Assert.AreEqual(0, slowInspector.SeenIps[0],
                "Both dispatch paths must expose instructionStart as the IP seen by the inspector.");
        }

        [TestMethod]
        public void SlowPath_Inspector_ConsistentAcrossMultipleInstructions()
        {
            // After a slow-path instruction at address 0 (3 bytes), the next instruction is at address 3.
            // The inspector must see IP = 3, not some intermediate value.
            var machine = new TestMachine();
            var inspector = new RecordingIpInspector();
            machine.Inspector = inspector;
            // Two PUSH_BYTE instructions: [0x01,0x01,0x10] at address 0, [0x01,0x01,0x01] at address 3
            byte[] program = [0x01, 0x01, 0x10,  0x01, 0x01, 0x01];
            machine.Execute(new DefaultContext(program));
            Assert.AreEqual(2, inspector.SeenIps.Count);
            Assert.AreEqual(0, inspector.SeenIps[0]);
            Assert.AreEqual(3, inspector.SeenIps[1]);
        }

        [TestMethod]
        public void SlowPath_OnBreakpointRedirect_BeforeInstructionNotCalledWithStaleIp()
        {
            // When OnBreakpoint redirects execution on a slow-path instruction, BeforeInstruction
            // must not be called. IP seen by OnBreakpoint must be instructionStart.
            int? seenIpInBreakpoint = null;
            bool beforeInstructionCalled = false;

            var machine = new TestMachine();
            machine.Breakpoints.Add(0);
            machine.Inspector = new ActionInspector(
                before: (ctx, addr, name) => beforeInstructionCalled = true,
                onBp: (ctx, addr, name) =>
                {
                    seenIpInBreakpoint = ctx.InstructionPointer;
                    ctx.InstructionPointer = ctx.Data.Length; // redirect to EOF
                });

            byte[] program = [0x01, 0x01, 0x42];
            machine.Execute(new DefaultContext(program));

            Assert.AreEqual(0, seenIpInBreakpoint, "OnBreakpoint must see IP = instructionStart for multi-byte opcodes.");
            Assert.IsFalse(beforeInstructionCalled, "BeforeInstruction must not be called after a breakpoint redirect.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal processor that records the IP seen by OnStep before each dispatch.
    /// Extends VirtualProcessor directly (not TestMachine) so that its own instruction
    /// methods are found by reflection — private methods of base classes are not returned
    /// by GetMethods on derived types.
    /// </summary>
    public class TrackingTestMachine : VirtualProcessor<DefaultContext>
    {
        private readonly System.Collections.Generic.List<int> _steps;

        [Instruction("PUSH_BYTE", 0x01, 0x01)]
        void PushByte(DefaultContext context, byte b1) => context.Stack.Push(b1);

        public TrackingTestMachine(System.Collections.Generic.List<int> steps) => _steps = steps;
        protected override void OnStep(DefaultContext context) => _steps.Add(context.InstructionPointer);
    }

    /// <summary>Minimal machine with only unambiguous single-byte opcodes (no multi-byte peers).</summary>
    public class InspectorMachine : VirtualProcessor<DefaultContext>
    {
        [Instruction("PUSH_A", 0x01)]
        void PushA(DefaultContext ctx) => ctx.Stack.Push(5);

        [Instruction("PUSH_B", 0x02)]
        void PushB(DefaultContext ctx) => ctx.Stack.Push(3);

        [Instruction("ADD", 0x10)]
        void Add(DefaultContext ctx) { var b = (int)ctx.Stack.Pop(); var a = (int)ctx.Stack.Pop(); ctx.Stack.Push(a + b); }
    }
}
