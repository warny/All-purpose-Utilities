using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO=System.IO;

namespace Utils.IO
{
	/// <summary>
	/// Ouvre une partie d'un stream
	/// </summary>
	public class PartialStream : System.IO.Stream
	{
		private readonly System.IO.Stream s;
		private readonly long startPosition;
		private long length;
		private long position;

		/// <summary>
		/// Ouvre une partie d'un stream à sa position actuelle en se limitant à la longueur indiquée
		/// </summary>
		/// <param name="s">Stream à ouvrir</param>
		/// <param name="length">Longueur à utiliser</param>
		public PartialStream(System.IO.Stream s, long length)
		{
			this.s = s;
			this.startPosition = s.Position;
			this.length = length;
			this.position = 0;
			Check();
		}

		/// <summary>
		/// Ouvre une partie d'un stream à la position indiquée en se limitant à la longueur indiquée
		/// </summary>
		/// <param name="s">Stream à ouvrir</param>
		/// <param name="position">Position à laquelle commencer la lecture</param>
		/// <param name="length">Longueur à utiliser</param>
		public PartialStream(System.IO.Stream s, long position, long length)
		{
			this.s = s;
			this.startPosition = position;
			this.length = length;
			this.position = 0;
			Check();
		}

		/// <summary>
		/// Vérifie sur le flux est parcourable
		/// </summary>
		private void Check()
		{
			if (!this.s.CanSeek) throw new ArgumentException("Le flux doit être parcourable");
		}

		public override bool CanRead => s.CanRead;
		public override bool CanSeek => s.CanSeek;
		public override bool CanWrite => s.CanWrite;

		public override long Length => length;

		public override long Position
		{
			get { return position; }
			set { position = value; }
		}

		public override void Flush()
		{
			s.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			lock (s)
			{
				long oldPosition = s.Position;
				s.Position = this.startPosition + this.position;
				if (this.position + offset + count > this.length)
				{
					count = (int)(this.length - this.position - offset);
				}
				var result = s.Read(buffer, offset, count);
				this.position = s.Position - this.startPosition;
				s.Position = oldPosition;
				return result;
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin:
					this.position = offset > 0 ? offset : 0;
					break;
				case SeekOrigin.Current:
					this.position = position + offset < 0 ? 0 : this.position + offset < this.length ? this.position + offset : this.length;
					break;
				case SeekOrigin.End:
					this.position = offset < length ? length - offset : length;
					break;
				default:
					break;
			}
			return this.position;
		}

		public override void SetLength(long value)
		{
			this.length = value;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			lock (s)
			{
				long oldPosition = s.Position;
				s.Position = this.startPosition + this.position;
				if (this.position + offset + count > this.length)
				{
					throw new ArgumentOutOfRangeException(nameof(count), "La taille de données à copier est trop longue");
				}
				s.Write(buffer, offset, count);
				this.position = s.Position - this.startPosition;
				s.Position = oldPosition;
			}
		}
	}
}
