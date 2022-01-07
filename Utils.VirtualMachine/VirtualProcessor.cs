using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Arrays;
using Utils.Collections;

namespace Utils.VirtualMachine
{
	public abstract class VirtualProcessor<T> where T : Context
	{
		public delegate void InstructionDelegate(T context);

		private int MaxDepth;

		protected Dictionary<IReadOnlyCollection<byte>, (string name, InstructionDelegate instruction)> InstructionsSet { get; }

		public VirtualProcessor()
		{
			InstructionsSet = ReadInstructionsSet();
		}

		public Dictionary<IReadOnlyCollection<byte>, (string name, InstructionDelegate instruction)> ReadInstructionsSet()
		{
			Type t = this.GetType();

			var result = new Dictionary<IReadOnlyCollection<byte>, (string name, InstructionDelegate instruction)>(ArrayEqualityComparers.Byte);

			var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

			foreach (var method in methods)
			{
				var instructionAttributes = method.GetCustomAttributes(typeof(InstructionAttribute), true);
				if (instructionAttributes.IsNullOrEmptyCollection()) { continue; }
				var instructionDelegate = (InstructionDelegate)method.CreateDelegate(typeof(InstructionDelegate), this);
				foreach (var instructionAttribute in instructionAttributes.OfType<InstructionAttribute>())
				{
					result.Add(instructionAttribute.Instruction, (instructionAttribute.Name, instructionDelegate));
					MaxDepth = Math.Max(instructionAttribute.Instruction.Length, MaxDepth);
				}
			}

			return result;
		}

		public void Execute(T context)
		{
			List<byte> currentInstruction = new List<byte>();
			while (context.IntructionPointer < context.Datas.Length)
			{
				bool found = false;
				currentInstruction.Clear();
				while (currentInstruction.Count < MaxDepth)
				{
					currentInstruction.Add(context.ReadByte());
					if (InstructionsSet.TryGetValue(currentInstruction, out var instruction))
					{
						Console.WriteLine(instruction.name);
						instruction.instruction(context);
						found = true;
						break;
					}
				}
				if (!found)
				{
					throw new ExecutionEngineException("Unknown instruction");
				}

			}
		}
	}

	public abstract class Context
	{
		public byte[] Datas { get; }
		public long IntructionPointer { get; set; }

		public Context(byte[] datas)
		{
			Datas = datas;
		}

		public byte ReadByte() => Datas[IntructionPointer++];
		public virtual Int16 ReadInt16() => (Int16)(ReadByte() << 8 | ReadByte());
		public virtual Int32 ReadInt32() => (Int32)ReadInt16() << 16 | (Int32)ReadInt16();
		public virtual Int64 ReadInt62() => (Int64)ReadInt32() << 32 | (Int64)ReadInt32();
	}

	public class DefaultContext : Context
	{
		public DefaultContext(byte[] datas) : base(datas)
		{
		}

		public Stack<object> Stack { get; } = new Stack<object>();
	}
}
