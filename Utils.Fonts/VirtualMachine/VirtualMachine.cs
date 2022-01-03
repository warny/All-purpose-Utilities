using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Utils.Arrays;
using Utils.IO.Serialization;
using Utils.Objects;

namespace Utils.VirtualMachine
{
	public abstract class VirtualMachine<T>
	{
		public delegate void InstructionDelegate(Reader reader, T context);

		private static readonly Type[] DelegateParametersTypes = new[] { typeof(Reader), typeof(T) };
		private int MaxDepth;

		protected Dictionary<IReadOnlyCollection<byte>, (string name, InstructionDelegate instruction)> InstructionsSet { get; }

		public VirtualMachine()
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

		public void Execute(Reader reader, T context)
		{
			List<byte> currentInstruction = new List<byte>();
			while (reader.BytesLeft > 0)
			{
				bool found = false;
				currentInstruction.Clear();
				while (currentInstruction.Count < MaxDepth)
				{
					currentInstruction.Add(reader.ReadByte());
					if (InstructionsSet.TryGetValue(currentInstruction, out var instruction))
					{
						Console.WriteLine(instruction.name);
						instruction.instruction(reader, context);
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

	public class DefaultContext
	{
		public Stack<object> Stack = new Stack<object>();
	}
}
