﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.VirtualMachine
{
        /// <summary>
        /// Provides methods to read numbers from a virtual machine context.
        /// </summary>
        public interface INumberReader
        {
                byte ReadByte(Context context);
                Int16 ReadInt16(Context context);
                Int32 ReadInt32(Context context);
                Int64 ReadInt64(Context context);
                UInt16 ReadUInt16(Context context);
                UInt32 ReadUInt32(Context context);
                UInt64 ReadUInt64(Context context);
                float ReadSingle(Context context);
                double ReadDouble(Context context);
        }

        /// <summary>
        /// Factory methods for obtaining <see cref="INumberReader"/> instances.
        /// </summary>
        public static class NumberReader
	{
		private static INumberReader NormalReader { get; } = new NormalReader();
		private static INumberReader InvertedReader { get; } = new InvertedReader();

                /// <summary>
                /// Returns a reader configured for the specified endianness.
                /// </summary>
                /// <param name="littleIndian">True for little-endian reading.</param>
                public static INumberReader GetReader(bool littleIndian)
                {
                        if (!littleIndian ^ BitConverter.IsLittleEndian)
                        {
                                return NormalReader;
                        }
                        else
                        {
                                return InvertedReader;
                        }

                }
	}

        /// <summary>
        /// Reader implementation matching the system endianness.
        /// </summary>
        internal class NormalReader : INumberReader
	{
		public byte ReadByte(Context context)
		{
			return context.Data[context.InstructionPointer++];
		}

		public short ReadInt16(Context context)
		{
			var result = BitConverter.ToInt16(context.Data, context.InstructionPointer);
			context.InstructionPointer += sizeof(Int16);
			return result;
		}

		public int ReadInt32(Context context)
		{
			var result = BitConverter.ToInt32(context.Data, context.InstructionPointer);
			context.InstructionPointer += sizeof(Int32);
			return result;
		}

		public long ReadInt64(Context context)
		{
			var result = BitConverter.ToInt64(context.Data, context.InstructionPointer);
			context.InstructionPointer += sizeof(Int64);
			return result;
		}

		public ushort ReadUInt16(Context context)
		{
			var result = BitConverter.ToUInt16(context.Data, context.InstructionPointer);
			context.InstructionPointer += sizeof(UInt16);
			return result;
		}

		public uint ReadUInt32(Context context)
		{
			var result = BitConverter.ToUInt32(context.Data, context.InstructionPointer);
			context.InstructionPointer += sizeof(UInt32);
			return result;
		}

		public ulong ReadUInt64(Context context)
		{
			var result = BitConverter.ToUInt64(context.Data, context.InstructionPointer);
			context.InstructionPointer += sizeof(UInt64);
			return result;
		}

		public float ReadSingle(Context context)
		{
			var result = BitConverter.ToSingle(context.Data, context.InstructionPointer);
			context.InstructionPointer += sizeof(Single);
			return result;
		}

		public double ReadDouble(Context context)
		{
			var result = BitConverter.ToDouble(context.Data, context.InstructionPointer);
			context.InstructionPointer += sizeof(Double);
			return result;
		}


	}

        /// <summary>
        /// Reader implementation that swaps endianness when reading values.
        /// </summary>
        internal class InvertedReader : INumberReader
	{
                /// <summary>
                /// Reads bytes from the context in reverse order to handle endian swapping.
                /// </summary>
                private static void ReadDatas(Context context, byte[] target)
                {
                        for (int i = target.Length - 1; i >= 0; i--)
                        {
                                target[i] = context.Data[context.InstructionPointer++];
                        }
                }

		public byte ReadByte(Context context)
		{
			return context.Data[context.InstructionPointer++];
		}

		public short ReadInt16(Context context)
		{
			var temp = new byte[sizeof(Int16)];
			ReadDatas(context, temp);
			return BitConverter.ToInt16(temp, 0);
		}

		public int ReadInt32(Context context)
		{
			var temp = new byte[sizeof(Int32)];
			ReadDatas(context, temp);
			return BitConverter.ToInt32(temp, 0);
		}

		public long ReadInt64(Context context)
		{
			var temp = new byte[sizeof(Int64)];
			ReadDatas(context, temp);
			return BitConverter.ToInt64(temp, 0);
		}

		public ushort ReadUInt16(Context context)
		{
			var temp = new byte[sizeof(UInt16)];
			ReadDatas(context, temp);
			return BitConverter.ToUInt16(temp, 0);
		}

		public uint ReadUInt32(Context context)
		{
			var temp = new byte[sizeof(UInt32)];
			ReadDatas(context, temp);
			return BitConverter.ToUInt32(temp, 0);
		}

		public ulong ReadUInt64(Context context)
		{
			var temp = new byte[sizeof(UInt64)];
			ReadDatas(context, temp);
			return BitConverter.ToUInt64(temp, 0);
		}

		public float ReadSingle(Context context)
		{
			var temp = new byte[sizeof(float)];
			ReadDatas(context, temp);
			return BitConverter.ToSingle(temp, 0);
		}

		public double ReadDouble(Context context)
		{
			var temp = new byte[sizeof(double)];
			ReadDatas(context, temp);
			return BitConverter.ToDouble(temp, 0);
		}
	}
}
