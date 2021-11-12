using System;
using System.Runtime.CompilerServices;
using Utils.IO.Serialization;

namespace Utils.Fonts.TTF
{
	public class LocaTable : TrueTypeTable
	{

		private int[] offsets;
		public virtual bool IsLongFormat { get; private set; }

		protected internal LocaTable() : base(TrueTypeTableTypes.loca) { }

		public override TrueTypeFont TrueTypeFont
		{
			get => base.TrueTypeFont;
			protected set
			{
				base.TrueTypeFont = value;
 				MaxpTable maxpTable = value.GetTable<MaxpTable>(TrueTypeTableTypes.maxp);
				HeadTable headTable = value.GetTable<HeadTable>(TrueTypeTableTypes.head);
				IsLongFormat = headTable.IndexToLocFormat == 1;
				offsets = new int[maxpTable.NumGlyphs + 1];
			}
		}

		public virtual int GetOffset(int i) => offsets[i];
		public virtual int GetSize(int i) => offsets[i + 1] - offsets[i];

		public override int Length
		{
			get {
				if (!IsLongFormat)
				{
					return offsets.Length * 2;
				}
				return offsets.Length * 4;
			}
		}


		public override void WriteData(Writer data)
		{
			for (int i = 0; i < offsets.Length; i++)
			{
				if (IsLongFormat)
				{
					data.WriteInt32(offsets[i], true);
				}
				else
				{
					data.WriteInt16((short)(offsets[i] / 2), true);
				}
			}
		}

		public override void ReadData(Reader data)
		{
			for (int i = 0; i < offsets.Length; i++)
			{
				if (IsLongFormat)
				{
					offsets[i] = data.ReadInt32(true);
				}
				else
				{
					offsets[i] = 2 * data.ReadInt16(true);
				}
			}
		}
	}
}
