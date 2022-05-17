using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Drawing
{
	public class Pathes : List<IDrawable>
	{
		public Pathes() { }

		public Pathes(ICollection<IDrawable> drawables) : base(drawables) { }
		public Pathes(params IDrawable[] drawables) : base(drawables) { }

		public void AddRange(params IDrawable[] drawables)
		{
			AddRange((IEnumerable<IDrawable>)drawables);
		}
	}
}
