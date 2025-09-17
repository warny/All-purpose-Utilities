using System;

namespace Utils.VirtualMachine
{
    /// <summary>
    /// Identifies a method as a virtual machine instruction and stores its metadata.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class InstructionAttribute : Attribute
    {
        /// <summary>
        /// Gets the human-readable name associated with the instruction.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the byte sequence representing the instruction opcode.
        /// </summary>
        public byte[] Instruction { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstructionAttribute"/> class.
        /// </summary>
        /// <param name="name">The descriptive name of the instruction.</param>
        /// <param name="instruction">The byte sequence that identifies the instruction.</param>
        public InstructionAttribute(string name, params byte[] instruction)
        {
            Name = name;
            Instruction = instruction;
        }
    }
}
