using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Utils.Arrays;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine
{
	public class TestMachine : VirtualProcessor<DefaultContext>
	{
		[Instruction("PUSH", 0x01, 0x01)]
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

	}
}
