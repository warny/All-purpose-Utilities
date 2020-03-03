﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.BaseEncoding
{
	public interface IBaseDescriptor
	{
		int this[char c] { get; }
		char this[int index] { get; }
		int BitsWidth { get; }
		string Separator { get; }
		char? Filler { get; }
		int FillerMod { get; }
	}

	public abstract class BaseDescriptorBase : IBaseDescriptor
	{
		private readonly char[] chars;
		private readonly Dictionary<char, int> reversed;

		protected BaseDescriptorBase(string chars, string separator, char? filler = null, int fillerMod = 0) : 
			this(chars.ToArray(), separator, filler, fillerMod) { }

		protected BaseDescriptorBase(char[] chars, string separator, char? filler = null, int fillerMod = 0)
		{
			this.chars = chars.ToArray();
			this.reversed = this.chars.Select((c, i) => new KeyValuePair<char, int>(c, i)).ToDictionary(kv => kv.Key, kv => kv.Value);
			this.Separator = separator ?? Environment.NewLine;
			this.Filler = filler;
			this.FillerMod = fillerMod;

			int depth = 0;
			int length = this.chars.Length;
			while (length > 1)
			{
				length = length >> 1;
				depth++;
			}
			if (length != 1) throw new ArgumentOutOfRangeException(nameof(chars), "Les caractères de transformations doivent avoir une longueur en puissance de 2");
			this.BitsWidth = depth;
		}

		public int this[char c] => reversed[c];
		public char this[int index] => chars[index];

		public int BitsWidth { get; }
		public string Separator { get; }
		public char? Filler { get; }
		public int FillerMod { get; }

	}

	public class Bases
	{
		public class Base16Descriptor : BaseDescriptorBase
		{
			public Base16Descriptor() : base("0123456789ABCDEF", Environment.NewLine, null) { }
		}

		public class Base32Descriptor : BaseDescriptorBase
		{
			public Base32Descriptor() : base("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567", Environment.NewLine, '=', 8) { }
		}

		public class Base64Descriptor : BaseDescriptorBase
		{
			public Base64Descriptor() : base("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/", Environment.NewLine, '=', 4) { }
		}

		public static IBaseDescriptor Base16 { get; } = new Base16Descriptor();
		public static IBaseDescriptor Base32 { get; } = new Base32Descriptor();
		public static IBaseDescriptor Base64 { get; } = new Base64Descriptor();
	}

}
