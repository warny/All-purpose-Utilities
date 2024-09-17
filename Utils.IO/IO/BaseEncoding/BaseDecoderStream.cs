using System;
using System.IO;
using System.Linq;
using System.Text;
using Utils.Mathematics;
using Utils.Objects;

namespace Utils.IO.BaseEncoding
{
	public class BaseDecoderStream : TextWriter
	{
		public Stream Stream { get; }
		protected IBaseDescriptor BaseDescriptor { get; }
		private readonly char[] toIgnore;

		public override Encoding Encoding { get; }

		int currentValue = 0;
		int dataLength = 0;

		public BaseDecoderStream(Stream stream, IBaseDescriptor baseDescriptor)
		{
			this.Stream = stream ?? throw new ArgumentNullException(nameof(stream));
			this.BaseDescriptor = baseDescriptor ?? throw new ArgumentNullException(nameof(baseDescriptor));

			toIgnore = BaseDescriptor.Filler is not null
				? BaseDescriptor.Separator.Union([' ', BaseDescriptor.Filler.Value]).ToArray()
				: BaseDescriptor.Separator.Union([' ']).ToArray();
		}

		int sourceLength = 0;
		int actualTargetLength = 0;
		public override void Write(char value)
		{
			if (value.In(toIgnore)) return;
			sourceLength++;
			int charValue = BaseDescriptor[value];
			currentValue = (currentValue << BaseDescriptor.BitsWidth) | charValue;

			dataLength += BaseDescriptor.BitsWidth;

			if (dataLength >= 8)
			{
				actualTargetLength++;
				dataLength -= 8;
				Stream.WriteByte((byte)((currentValue >> dataLength) & 0xFF));
			}
		}

		public override void Close()
		{
			if (dataLength > 0)
			{
				int targetLength = (int)Math.Floor(sourceLength * BaseDescriptor.BitsWidth / 8d);
				if (actualTargetLength > targetLength)
				{
					currentValue = currentValue << BaseDescriptor.BitsWidth;
					dataLength += BaseDescriptor.BitsWidth - 8;
					Stream.WriteByte((byte)((currentValue >> dataLength) & 0xFF));
				}
			}
			Flush();
		}

		public override void Flush()
		{
			Stream.Flush();
		}
	}
}
