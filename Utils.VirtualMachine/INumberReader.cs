using System;

namespace Utils.VirtualMachine
{
    /// <summary>
    /// Provides methods to read numbers from a virtual machine <see cref="Context"/>.
    /// </summary>
    public interface INumberReader
    {
        /// <summary>
        /// Reads a single byte from the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <returns>The next byte available in the context.</returns>
        byte ReadByte(Context context);

        /// <summary>
        /// Reads a 16-bit signed integer from the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <returns>The next <see cref="short"/> value available in the context.</returns>
        short ReadInt16(Context context);

        /// <summary>
        /// Reads a 32-bit signed integer from the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <returns>The next <see cref="int"/> value available in the context.</returns>
        int ReadInt32(Context context);

        /// <summary>
        /// Reads a 64-bit signed integer from the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <returns>The next <see cref="long"/> value available in the context.</returns>
        long ReadInt64(Context context);

        /// <summary>
        /// Reads a 16-bit unsigned integer from the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <returns>The next <see cref="ushort"/> value available in the context.</returns>
        ushort ReadUInt16(Context context);

        /// <summary>
        /// Reads a 32-bit unsigned integer from the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <returns>The next <see cref="uint"/> value available in the context.</returns>
        uint ReadUInt32(Context context);

        /// <summary>
        /// Reads a 64-bit unsigned integer from the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <returns>The next <see cref="ulong"/> value available in the context.</returns>
        ulong ReadUInt64(Context context);

        /// <summary>
        /// Reads a single-precision floating-point value from the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <returns>The next <see cref="float"/> value available in the context.</returns>
        float ReadSingle(Context context);

        /// <summary>
        /// Reads a double-precision floating-point value from the supplied <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <returns>The next <see cref="double"/> value available in the context.</returns>
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
        /// <param name="littleEndian">True for little-endian reading; otherwise, false.</param>
        /// <returns>An <see cref="INumberReader"/> that reads values using the desired byte order.</returns>
        public static INumberReader GetReader(bool littleEndian)
        {
            if (!littleEndian ^ BitConverter.IsLittleEndian)
            {
                return NormalReader;
            }

            return InvertedReader;
        }
    }

    /// <summary>
    /// Reader implementation matching the system endianness.
    /// </summary>
    internal class NormalReader : INumberReader
    {
        /// <inheritdoc />
        public byte ReadByte(Context context)
        {
            return context.Data[context.InstructionPointer++];
        }

        /// <inheritdoc />
        public short ReadInt16(Context context)
        {
            var result = BitConverter.ToInt16(context.Data, context.InstructionPointer);
            context.InstructionPointer += sizeof(short);
            return result;
        }

        /// <inheritdoc />
        public int ReadInt32(Context context)
        {
            var result = BitConverter.ToInt32(context.Data, context.InstructionPointer);
            context.InstructionPointer += sizeof(int);
            return result;
        }

        /// <inheritdoc />
        public long ReadInt64(Context context)
        {
            var result = BitConverter.ToInt64(context.Data, context.InstructionPointer);
            context.InstructionPointer += sizeof(long);
            return result;
        }

        /// <inheritdoc />
        public ushort ReadUInt16(Context context)
        {
            var result = BitConverter.ToUInt16(context.Data, context.InstructionPointer);
            context.InstructionPointer += sizeof(ushort);
            return result;
        }

        /// <inheritdoc />
        public uint ReadUInt32(Context context)
        {
            var result = BitConverter.ToUInt32(context.Data, context.InstructionPointer);
            context.InstructionPointer += sizeof(uint);
            return result;
        }

        /// <inheritdoc />
        public ulong ReadUInt64(Context context)
        {
            var result = BitConverter.ToUInt64(context.Data, context.InstructionPointer);
            context.InstructionPointer += sizeof(ulong);
            return result;
        }

        /// <inheritdoc />
        public float ReadSingle(Context context)
        {
            var result = BitConverter.ToSingle(context.Data, context.InstructionPointer);
            context.InstructionPointer += sizeof(float);
            return result;
        }

        /// <inheritdoc />
        public double ReadDouble(Context context)
        {
            var result = BitConverter.ToDouble(context.Data, context.InstructionPointer);
            context.InstructionPointer += sizeof(double);
            return result;
        }
    }

    /// <summary>
    /// Reader implementation that swaps endianness when reading values.
    /// </summary>
    internal class InvertedReader : INumberReader
    {
        /// <summary>
        /// Copies bytes from the context into <paramref name="target"/> in reversed order.
        /// </summary>
        /// <param name="context">The execution context containing the instruction stream.</param>
        /// <param name="target">The buffer receiving the bytes in reversed order.</param>
        private static void ReadDatas(Context context, byte[] target)
        {
            for (int i = target.Length - 1; i >= 0; i--)
            {
                target[i] = context.Data[context.InstructionPointer++];
            }
        }

        /// <inheritdoc />
        public byte ReadByte(Context context)
        {
            return context.Data[context.InstructionPointer++];
        }

        /// <inheritdoc />
        public short ReadInt16(Context context)
        {
            var temp = new byte[sizeof(short)];
            ReadDatas(context, temp);
            return BitConverter.ToInt16(temp, 0);
        }

        /// <inheritdoc />
        public int ReadInt32(Context context)
        {
            var temp = new byte[sizeof(int)];
            ReadDatas(context, temp);
            return BitConverter.ToInt32(temp, 0);
        }

        /// <inheritdoc />
        public long ReadInt64(Context context)
        {
            var temp = new byte[sizeof(long)];
            ReadDatas(context, temp);
            return BitConverter.ToInt64(temp, 0);
        }

        /// <inheritdoc />
        public ushort ReadUInt16(Context context)
        {
            var temp = new byte[sizeof(ushort)];
            ReadDatas(context, temp);
            return BitConverter.ToUInt16(temp, 0);
        }

        /// <inheritdoc />
        public uint ReadUInt32(Context context)
        {
            var temp = new byte[sizeof(uint)];
            ReadDatas(context, temp);
            return BitConverter.ToUInt32(temp, 0);
        }

        /// <inheritdoc />
        public ulong ReadUInt64(Context context)
        {
            var temp = new byte[sizeof(ulong)];
            ReadDatas(context, temp);
            return BitConverter.ToUInt64(temp, 0);
        }

        /// <inheritdoc />
        public float ReadSingle(Context context)
        {
            var temp = new byte[sizeof(float)];
            ReadDatas(context, temp);
            return BitConverter.ToSingle(temp, 0);
        }

        /// <inheritdoc />
        public double ReadDouble(Context context)
        {
            var temp = new byte[sizeof(double)];
            ReadDatas(context, temp);
            return BitConverter.ToDouble(temp, 0);
        }
    }
}
