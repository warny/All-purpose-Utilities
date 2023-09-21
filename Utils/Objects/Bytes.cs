using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Utils.Arrays;

namespace Utils.Objects
{
	/// <summary>
	/// Read only bytes structure
	/// </summary>
	public struct Bytes :
		IReadOnlyList<byte>, IReadOnlyCollection<byte>,
		IEnumerable<byte>, ICloneable, 
		IEquatable<Bytes>, IEquatable<byte[]>,
		IComparable<Bytes>, IComparable<byte[]>, IComparable		
	{
		static ArrayComparer<byte> comparer = new ArrayComparer<byte>();

		readonly byte[] innerBytes;

		public int Count => innerBytes.Length;

		internal Bytes(params byte[] byteArray)
		{
			this.innerBytes = byteArray;
		}

		internal Bytes(params byte[][] byteArrays)
		{
			this.innerBytes = new byte[byteArrays.Sum(a => a.Length)];
			int position = 0;
			foreach (var item in byteArrays)
			{
				Array.Copy(item, this.innerBytes, position);
				position += item.Length;
			}
		}

		internal Bytes(params Bytes[] bytess)
		{
			this.innerBytes = new byte[bytess.Sum(a=>a.innerBytes.Length)];
			int position = 0;
			foreach (var item in bytess)
			{
				Array.Copy(item.innerBytes, this.innerBytes, position);
				position += item.innerBytes.Length;
			}
		}

		public byte[] ToArray()
		{
			byte[] result = new byte[this.innerBytes.Length];
			Array.Copy(this.innerBytes, result, this.innerBytes.Length);
			return result;
		}

		public IEnumerator<byte> GetEnumerator() => ((IEnumerable<byte>)innerBytes).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => innerBytes.GetEnumerator();

		public byte this[int index] => this.innerBytes[index];

		#region operators
		public static implicit operator Bytes (byte[] bytes) => new Bytes(bytes);
		public static implicit operator byte[] (Bytes bytes) => bytes.ToArray();
		public static bool operator ==(Bytes left, Bytes right) => left.Equals(right);
		public static bool operator ==(Bytes left, byte[] right) => left.Equals(right);
		public static bool operator ==(byte[] left, Bytes right) => right.Equals(left);
		public static bool operator !=(Bytes left, Bytes right) => !left.Equals(right);
		public static bool operator !=(Bytes left, byte[] right) => !left.Equals(right);
		public static bool operator !=(byte[] left, Bytes right) => !right.Equals(left);

		public static bool operator > (Bytes left, Bytes right) => left.CompareTo(right) == 1;
		public static bool operator > (Bytes left, byte[] right) => left.CompareTo(right) == 1;
		public static bool operator > (byte[] left, Bytes right) => right.CompareTo(left) == -1;
		public static bool operator <(Bytes left, Bytes right) => left.CompareTo(right) == -1;
		public static bool operator <(Bytes left, byte[] right) => left.CompareTo(right) == -1;
		public static bool operator <(byte[] left, Bytes right) => right.CompareTo(left) == 1;

		public static bool operator >=(Bytes left, Bytes right) => left.CompareTo(right) != -1;
		public static bool operator >=(Bytes left, byte[] right) => left.CompareTo(right) != 11;
		public static bool operator >=(byte[] left, Bytes right) => right.CompareTo(left) != 1;
		public static bool operator <=(Bytes left, Bytes right) => left.CompareTo(right) != 1;
		public static bool operator <=(Bytes left, byte[] right) => left.CompareTo(right) != 1;
		public static bool operator <=(byte[] left, Bytes right) => right.CompareTo(left) != -1;

		public static Bytes operator +(Bytes left, Bytes right) => new Bytes(left, right);
		public static Bytes operator +(Bytes left, byte[] right) => new Bytes(left.innerBytes, right);
		public static Bytes operator +(byte[] left, Bytes right) => new Bytes(left, right.innerBytes);
		#endregion

		public override int GetHashCode() => ObjectUtils.ComputeHash(this.innerBytes);
		
		public override bool Equals(object obj)
		{
			switch (obj)
			{
				case Bytes b: return Equals(b);
				case byte[] a: return Equals(a);
				default: return false;
			}
		}
		public bool Equals(Bytes other) => comparer.Compare(this.innerBytes, other.innerBytes) == 0;
		public bool Equals(params byte[] other) => comparer.Compare(this.innerBytes, other) == 0;

		public int CompareTo(object obj)
		{
			switch (obj)
			{
				case Bytes b: return CompareTo(b);
				case byte[] a: return CompareTo(a);
				default: throw new InvalidOperationException();
			}
		}
		public int CompareTo(Bytes other) => comparer.Compare(this.innerBytes, other.innerBytes);
		public int CompareTo(params byte[] other) => comparer.Compare(this.innerBytes, other);

		public object Clone() => new Bytes(this);

		public void CopyTo(byte[] array, int index, int? length = null)
		{
			index.ArgMustBeLesserOrEqualsThan(array.Length);
			if (index < 0) index = array.Length + index;
			index.ArgMustBeGreaterOrEqualsThan(0);
			if (length is null || length + index > array.Length) length = Math.Min(array.Length - index, this.innerBytes.Length);
			Array.Copy(this.innerBytes, 0, array, index, length.Value);
		}

		public void CopyTo(Stream s, int index, int? length =  null) 
		{
			length ??= this.Count;
			s.Write(this.innerBytes, index, length.Value);
		}

		public override string ToString() => string.Join(" ", innerBytes.Select(b => b.ToString("X2")));

		public static Bytes Parse(string s)
		{
			var values = s.Split(new[] { ' ', '\n', '\t', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			return new Bytes(values.Select(v => byte.Parse(v, System.Globalization.NumberStyles.HexNumber)).ToArray());
		}
	}

	public static class BytesExtensions
	{
		public static Bytes ToBytes(this byte[] byteArray)
		{
            var innerBytes = new byte[byteArray.Length];
            Array.Copy(byteArray, innerBytes, 0);
			return new Bytes(innerBytes);
        }

		public static Bytes ToBytes( this IEnumerable<byte> byteArray)
		{
			return new Bytes(byteArray.ToArray());
		}

		public static Bytes AsBytes(this byte[] byteArray)
		{
			return new Bytes(byteArray);
		}

        public static Bytes Join(IEnumerable<byte[]> byteArrays) => Join(byteArrays.ToArray());
		public static Bytes Join(params byte[][] byteArrays)
        {
            var innerBytes = new byte[byteArrays.Sum(a => a.Length)];
            int position = 0;
            foreach (var item in byteArrays)
            {
                Array.Copy(item, innerBytes, position);
                position += item.Length;
            }
            return new Bytes(innerBytes);
        }

        public static Bytes Join(IEnumerable<Bytes> byteArrays) => Join(byteArrays.ToArray());
        public static Bytes Join(params Bytes[] byteArrays)
        {
            var innerBytes = new byte[byteArrays.Sum(a => a.Count)];
            int position = 0;
            foreach (var item in byteArrays)
            {
                Array.Copy(item, innerBytes, position);
                position += item.Count;
            }
            return new Bytes(innerBytes);
        }

        public static Bytes Join(IEnumerable<IEnumerable<byte>> byteArrays)
        {
			MemoryStream memory = new();
			foreach (var byteArray in byteArrays)
			{
				foreach (var value in byteArray)
				{
					memory.WriteByte(value);
				}
			}
			return new Bytes(Join(memory.ToArray()));
        }

    }
}
