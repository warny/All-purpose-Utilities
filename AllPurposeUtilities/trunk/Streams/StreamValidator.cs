using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO = System.IO;

namespace Utils
{
	public class StreamValidator : IO.Stream
	{
		private byte[] buffer = new byte[65536];
		private int length;

		private readonly IO.Stream target;

		public StreamValidator( IO.Stream target )
		{
			this.target = target;
		}

		public override bool CanRead => false;
		public override bool CanSeek => false;
		public override bool CanWrite => true;

		public override long Length=>this.target.Length;

		public override long Position
		{
			get { return this.target.Position; }

			set { throw new NotSupportedException(); }
		}

		public override void Flush()
		{
		}

		public void Validate()
		{
			target.Write(this.buffer, 0, this.length);
			this.length = 0;
		}

		public void Discard()
		{
			this.length = 0;
		}

		public override int Read( byte[] buffer, int offset, int count )
		{
			throw new NotSupportedException();
		}

		public override long Seek( long offset, SeekOrigin origin )
		{
			throw new NotSupportedException();
		}

		public override void SetLength( long value )
		{
			throw new NotSupportedException();
		}

		public override void Write( byte[] buffer, int offset, int count )
		{
			int nextPosition = this.length + count;
			if (nextPosition > this.buffer.Length) {
				int newlength = buffer.Length;
				while (newlength < nextPosition) {
					newlength *= 2;
				}
				byte[] newBuffer = new byte[newlength];
				Array.Copy(this.buffer, newBuffer, this.length);
				this.buffer = newBuffer;
			}

			Array.Copy(buffer, offset, this.buffer, this.length, count);
			this.length = nextPosition;
		}
	}
}
