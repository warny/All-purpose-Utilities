using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;
using Utils.Arrays;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine
{
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

            var context = new DefaultContext([0xFF]);
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
    }
}
