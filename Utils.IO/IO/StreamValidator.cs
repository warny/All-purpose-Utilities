using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO = System.IO;

namespace Utils
{
	/// <summary>
	/// Flux tampon dans lequel on peut écrire des données qui ne sont écrite dans le flux final 
	/// qu'en cas de validation avec l'appel de la fonction <see cref="Validate">Validate</see>
	/// </summary>
	public class StreamValidator : System.IO.Stream
	{
		private byte[] buffer = new byte[65536];
		private int length;

		private readonly System.IO.Stream target;

		public StreamValidator(System.IO.Stream target )
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

		/// <summary>
		/// Valide les données dans le tampon et les copie dans le flux final
		/// </summary>
		public void Validate()
		{
			target.Write(this.buffer, 0, this.length);
			this.length = 0;
		}

		/// <summary>
		/// Supprime les données du tampon sans les copier dans le flux final
		/// </summary>
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
