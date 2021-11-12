using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Arrays;

namespace Utils.Fonts
{
	/// <summary>
	/// 4 magicbytes decoder
	/// </summary>
	public struct Tag : IEquatable<Tag>, IEquatable<int>, IEquatable<string>, IEquatable<byte[]>
	{
		public string Name { get; }
		public int Value { get; }

		public Tag(string name)
		{
			if (name.Length > 4) throw new ArgumentException("name must be 4 characters long", nameof(name));
			name = name.PadRight(4, ' ');
			Name = name;
			Value = (name[0] << 24) | (name[1] << 16) | (name[2] << 8) | name[3];
		}

		public Tag(byte[] value)
		{
			Value = (value[0] << 24) | (value[1] << 16) | (value[2] << 8) | value[3];
			Name = new string(new char[]
		{
				(char)value[0],
				(char)value[1],
				(char)value[2],
				(char)value[3]
			});
		}

		public Tag(int value)
		{
			Value = value;
			Name = new string(new char[]
			{
				(char)(0xFF & (value >> 24)),
				(char)(0xFF & (value >> 16)),
				(char)(0xFF & (value >> 8)),
				(char)(0xFF & value)
			});
		}

		public override string ToString() => $"({Name},{Value:X2})";

		public bool Equals(Tag other) => this.Value == other.Value;
		public bool Equals(string other) => this.Name == other;
		public bool Equals(int other) => this.Value == other;
		public bool Equals(byte[] other) => this.Name[0] == (char)other[0] && this.Name[1] == (char)other[1] && this.Name[2] == (char)other[2] && this.Name[3] == (char)other[3];
		public override bool Equals(object obj) =>
			(obj is Tag tag && Equals(tag)) ||
			(obj is string str && Equals(str)) ||
			(obj is int i && Equals(i));
		public override int GetHashCode() => this.Value;

		public static implicit operator string(Tag tag) => tag.Name;
		public static implicit operator int(Tag tag) => tag.Value;
		public static implicit operator byte[](Tag tag) => new byte[] { (byte)tag.Name[0], (byte)tag.Name[1], (byte)tag.Name[2], (byte)tag.Name[3] };
		public static implicit operator Tag(string name) => new Tag(name);
		public static implicit operator Tag(int value) => new Tag(value);
		public static implicit operator Tag(byte[] value) => new Tag(value);

		public static bool operator ==(Tag tag1, Tag tag2) => tag1.Equals(tag2);
		public static bool operator !=(Tag tag1, Tag tag2) => !tag1.Equals(tag2);
		public static bool operator ==(Tag tag1, string tag2) => tag1.Equals(tag2);
		public static bool operator !=(Tag tag1, string tag2) => !tag1.Equals(tag2);
		public static bool operator ==(Tag tag1, int tag2) => tag1.Equals(tag2);
		public static bool operator !=(Tag tag1, int tag2) => !tag1.Equals(tag2);
		public static bool operator ==(Tag tag1, byte[] tag2) => tag1.Equals(tag2);
		public static bool operator !=(Tag tag1, byte[] tag2) => !tag1.Equals(tag2);
		public static bool operator ==(string tag1, Tag tag2) => tag2.Equals(tag1);
		public static bool operator !=(string tag1, Tag tag2) => !tag2.Equals(tag1);
		public static bool operator ==(int tag1, Tag tag2) => tag2.Equals(tag1);
		public static bool operator !=(int tag1, Tag tag2) => !tag2.Equals(tag1);
		public static bool operator ==(byte[] tag1, Tag tag2) => tag2.Equals(tag1);
		public static bool operator !=(byte[] tag1, Tag tag2) => !tag2.Equals(tag1);

	}
}
