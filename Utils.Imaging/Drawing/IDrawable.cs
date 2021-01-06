using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Drawing
{
	public interface IDrawable
	{
		float Length { get; }
		IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0);
	}
}
