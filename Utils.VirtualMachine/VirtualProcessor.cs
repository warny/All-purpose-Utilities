using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Arrays;
using Utils.Collections;

namespace Utils.VirtualMachine
{
	public abstract class VirtualProcessor<T> where T : Context
	{
		private readonly INumberReader numberReader;

		public delegate void InstructionDelegate(T context);

		private int MaxDepth;

		protected Dictionary<IReadOnlyCollection<byte>, (string name, InstructionDelegate instruction)> InstructionsSet { get; }

		public VirtualProcessor(bool littleIndian = true)
		{
			numberReader = NumberReader.GetReader(littleIndian);
			InstructionsSet = ReadInstructionsSet();
		}

		private Dictionary<IReadOnlyCollection<byte>, (string name, InstructionDelegate instruction)> ReadInstructionsSet()
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

		protected byte ReadByte(Context context) => numberReader.ReadByte(context);
		protected Int16 ReadInt16(Context context) => numberReader.ReadInt16(context);
		protected Int32 ReadInt32(Context context) => numberReader.ReadInt32(context);
		protected Int64 ReadInt64(Context context) => numberReader.ReadInt64(context);
		protected UInt16 ReadUInt16(Context context) => numberReader.ReadUInt16(context);
		protected UInt32 ReadUInt32(Context context) => numberReader.ReadUInt32(context);
		protected UInt64 ReadUInt64(Context context) => numberReader.ReadUInt64(context);
		protected float ReadSingle(Context context) => numberReader.ReadSingle(context);
		protected double ReadDouble(Context context) => numberReader.ReadDouble(context);


		public void Execute(T context)
		{
			List<byte> currentInstruction = new List<byte>();
			while (context.IntructionPointer < context.Datas.Length)
			{
				bool found = false;
				currentInstruction.Clear();
				while (currentInstruction.Count < MaxDepth)
				{
					currentInstruction.Add(ReadByte(context));
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
					throw new VirtualProcessorException("Unknown instruction");
				}

			}
		}
	}

	public abstract class Context
	{
		public byte[] Datas { get; }
		public int IntructionPointer { get; set; }

		public Context(byte[] datas)
		{
			Datas = datas;
		}

	}

	public class DefaultContext : Context
	{
		public DefaultContext(byte[] datas) : base(datas)
		{
		}

		public Stack<object> Stack { get; } = new Stack<object>();
	}
}
