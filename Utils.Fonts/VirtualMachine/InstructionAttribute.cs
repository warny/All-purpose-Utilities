using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.VirtualMachine
{
	[AttributeUsage(AttributeTargets.Method)]
	public class InstructionAttribute : Attribute
	{
		public string Name { get; }
		public byte[] Instruction { get; }

		public InstructionAttribute(string name, params byte[] instruction)
		{
			Name = name;
			Instruction = instruction;
		}
	}
}
