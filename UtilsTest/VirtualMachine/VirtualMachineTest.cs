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
	public class TestMachine : VirtualMachine<DefaultContext> {
		[Instruction("PUSH", 0x01)]
		void Push(Reader reader, DefaultContext context)
		{
			context.Stack.Push (reader.ReadByte());
		}

		[Instruction("POP", 0x02)]
		void Pop(Reader reader, DefaultContext context)
		{
			context.Stack.Pop();
		}

		[Instruction("ADD", 0X10, 0x01)]
		void Add(Reader reader, DefaultContext context)
		{
			var op2 = (byte)context.Stack.Pop();
			var op1 = (byte)context.Stack.Pop();
			var res = op1 + op2;
			context.Stack.Push ((byte)res);
		}

		[Instruction("SUB", 0X10, 0x02)]
		void Substract(Reader reader, DefaultContext context)
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
			
			Reader reader = new Reader(new MemoryStream(instructions));

			var context = new DefaultContext();
			TestMachine machine = new TestMachine();
			machine.Execute(reader, context);

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
			Reader reader = new Reader(new MemoryStream(instructions));

			var context = new DefaultContext();
			TestMachine machine = new TestMachine();
			machine.Execute(reader, context);

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
			Reader reader = new Reader(new MemoryStream(instructions));

			var context = new DefaultContext();
			TestMachine machine = new TestMachine();
			machine.Execute(reader, context);

			var result = context.Stack.OfType<byte>().ToArray();
			Assert.IsTrue(ArrayEqualityComparers.Byte.Equals(new byte[] { 0x0F }, result));
		}
	}
}
