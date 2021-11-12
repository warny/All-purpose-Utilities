namespace Utils.Fonts
{
	public class PointF
	{
		public float X { get; set; }

		public float Y { get; set; }

		public bool Open { get; set; }

		public PointF()
		{
			X = 0f;
			Y = 0f;
			Open = false;
		}

		public void Reset()
		{
			X = 0f;
			Y = 0f;
			Open = false;
		}
	}
}
