using System;
using System.Collections.Generic;
using System.Text;
using Utils.Imaging;

namespace Utils.Drawing
{
	public class BaseDrawing<T>
	{
		public IImageAccessor<T> ImageAccessor { get; }
		public BaseDrawing(IImageAccessor<T> imageAccessor)
		{
			ImageAccessor = imageAccessor;
		}

	}
}
