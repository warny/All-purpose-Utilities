using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO = System.IO;

namespace Utils.Streams
{
	public class StreamCopier : IO.Stream, IList<IO.Stream>
	{
		private readonly List<IO.Stream> targets;

		public override bool CanRead => false;
		public override bool CanSeek => false;
		public override bool CanWrite => true;

		public StreamCopier()
		{
			targets = new List<IO.Stream>();
		}
		public StreamCopier( IEnumerable<IO.Stream> streams )
		{
			targets = new List<IO.Stream>();
			targets.AddRange(streams);
		}

		public StreamCopier(params IO.Stream[] streams) : this((IEnumerable<IO.Stream>)streams) { }

		public override long Length
		{
			get { throw new NotSupportedException(); }
		}

		public override long Position
		{
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		public int Count => targets.Count;

		public bool IsReadOnly => false;

		public IO.Stream this[int index]
		{
			get { return targets[index]; }
			set { targets[index] = value; }
		}

		public override void Flush()
		{
			targets.ForEach(s => s.Flush());
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
			targets.ForEach(s => s.Write(buffer, offset, count));
		}

		public int IndexOf( IO.Stream item )
		{
			return targets.IndexOf(item);
		}

		public void Insert( int index, IO.Stream item )
		{
			targets.Insert(index, item);
		}

		public void RemoveAt( int index )
		{
			targets.RemoveAt(index);
		}

		public void Add( IO.Stream item )
		{
			targets.Add(item);
		}

		public void Clear()
		{
			targets.Clear();
		}

		public bool Contains( IO.Stream item )
		{
			return targets.Contains(item);
		}

		public void CopyTo( IO.Stream[] array, int arrayIndex )
		{
			targets.CopyTo(array, arrayIndex);
		}

		public bool Remove( IO.Stream item )
		{
			return targets.Remove(item);
		}

		public IEnumerator<IO.Stream> GetEnumerator()
		{
			return targets.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return targets.GetEnumerator();
		}
	}
}
