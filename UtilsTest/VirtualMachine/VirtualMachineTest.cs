using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.IO.Serialization;
using Utils.VirtualMachine;

namespace UtilsTest.VirtualMachine
{
	public class TestMachine : VirtualProcessor<DefaultContext>
	{
		[Instruction("PUSH", 0x01)]
		void Push(DefaultContext context)
		{
			context.Stack.Push(ReadByte(context));
		}

		[Instruction("POP", 0x02)]
		void Pop(DefaultContext context)
		{
			context.Stack.Pop();
		}

		[Instruction("ADD", 0X10, 0x01)]
		void Add(DefaultContext context)
		{
			var op2 = (byte)context.Stack.Pop();
			var op1 = (byte)context.Stack.Pop();
			var res = op1 + op2;
			context.Stack.Push((byte)res);
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
	public class VirtualMachineTest
	{
		[TestMethod]
		public void Test1()
		{
			byte[] instructions = new byte[] { 
				//Push 0x10
				0x01, 0x10, 
				//Push 0x01
				0x01, 0x01
			};

			var context = new DefaultContext(instructions);
			TestMachine machine = new TestMachine();
			machine.Execute(context);

			var result = context.Stack.OfType<byte>().ToArray();
			Assert.IsTrue(ArrayEqualityComparers.Byte.Equals(new byte[] { 0x01, 0x10 }, result));
		}

		[TestMethod]
		public void Test2()
		{
			byte[] instructions = new byte[] { 
				//Push 0x10
				0x01, 0x10, 
				//Push 0x01
				0x01, 0x01,
				//Add 
				0x10, 0x01
			};

			var context = new DefaultContext(instructions);
			TestMachine machine = new TestMachine();
			machine.Execute(context);

			var result = context.Stack.OfType<byte>().ToArray();
			Assert.IsTrue(ArrayEqualityComparers.Byte.Equals(new byte[] { 0x11 }, result));
		}

		[TestMethod]
		public void Test3()
		{
			byte[] instructions = new byte[] { 
				//Push 0x10
				0x01, 0x10, 
				//Push 0x01
				0x01, 0x01,
				//Substract 
				0x10, 0x02
			};

			var context = new DefaultContext(instructions);
			TestMachine machine = new TestMachine();
			machine.Execute(context);

			var result = context.Stack.OfType<byte>().ToArray();
			Assert.IsTrue(ArrayEqualityComparers.Byte.Equals(new byte[] { 0x0F }, result));
		}
	}
}
