using System;
using System.IO;

namespace Utils.BaseEncoding
{
	public class BaseEncoderStream : Stream
	{
		private int position = 0;
		private int targetPosition = 0;

		public TextWriter TargetWriter { get; }
		protected IBaseDescriptor BaseDescriptor { get; }
		public int MaxDataWidth { get; }
		public int Indent { get; }

		private int Depth { get; }
		private int Mask { get; }

		public override bool CanRead => false;
		public override bool CanSeek => false;
		public override bool CanWrite => true;
		public override long Length { get; }
		public override long Position {
			get => position;
			set => throw new InvalidOperationException();
		}

		public BaseEncoderStream(TextWriter targetWriter, IBaseDescriptor baseDescriptor, int maxDataWidth = -1, int indent = 0)
		{
			TargetWriter = targetWriter ?? throw new NullReferenceException(nameof(targetWriter));
			BaseDescriptor = baseDescriptor ?? throw new NullReferenceException(nameof(baseDescriptor));
			MaxDataWidth = maxDataWidth;
			Indent = indent;

			Depth = BaseDescriptor.BitsWidth;
			Mask = 0;
			for (int i = 0; i < Depth; i++) Mask |= 1 << i;
		}


		public override void Flush()
		{
			TargetWriter.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new InvalidOperationException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new InvalidOperationException();
		}

		public override void SetLength(long value)
		{
			throw new InvalidOperationException();
		}

		private int value = 0;
		private int shift = 0;
		private int dataWidth = 0;

		public override void Write(byte[] buffer, int offset, int count)
		{
			foreach (var b in buffer)
			{
				position++;
				value = (value << 8) | b;
				shift += 8;
				while (shift >= Depth)
				{
					shift -= Depth;
					targetPosition++;
					var charIndex = (value >> shift) & Mask;
					TargetWriter.Write(BaseDescriptor[charIndex]);
					if (MaxDataWidth != -1)
					{
						dataWidth++;
						if (dataWidth > MaxDataWidth)
						{
							dataWidth = 0;
							TargetWriter.Write(BaseDescriptor.Separator);
							TargetWriter.Write(new string(' ', Indent));
						}
					}
				}
			}
		}

		public override void Close()
		{
			if (shift > 0)
			{
				var charIndex = (value << (Depth - shift)) & Mask;
				TargetWriter.Write(BaseDescriptor[charIndex]);
			}

			if (BaseDescriptor.Filler != null && targetPosition % BaseDescriptor.FillerMod != 0)
			{
				int toFill = BaseDescriptor.FillerMod - (targetPosition % BaseDescriptor.FillerMod) - 1;
				TargetWriter.Write(new string(BaseDescriptor.Filler.Value, toFill));
			}

			this.Flush();
			base.Close();
		}
	}
}
