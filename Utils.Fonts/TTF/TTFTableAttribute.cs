using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utils.Fonts.TTF
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class TTFTableAttribute : Attribute
	{
		public TTFTableAttribute(TrueTypeTableTypes.Tags tableTag, params TrueTypeTableTypes.Tags[] dependsOn)
		{
			TableTag = new Tag((int)tableTag);
			DependsOn = dependsOn.Select(d=>new Tag((int)d)).ToArray() ?? new Tag[0];
		}

		public Tag TableTag { get; }
		public Tag[] DependsOn { get; }
	}
}
