namespace Utils.Drawing
{
	public class DrawPoint
	{
		public DrawPoint(int x, int y, int horizontalDirection, int verticalDirection, float sin, float cos, float position)
		{
			X = x;
			Y = y;
			HorizontalDirection = horizontalDirection;
			VerticalDirection = verticalDirection;
			Sin = sin;
			Cos = cos;
			Position = position;
		}

		public int X { get; }
		public int Y { get; }
		public int HorizontalDirection { get; }
		public int VerticalDirection { get; }
		public float Sin { get; }
		public float Cos { get; }
		public float Position { get; }

		public override string ToString() {
			string vDir, hDir;
			switch (HorizontalDirection)
			{
				case -1:
					hDir = "◄";
					break;
				case 1:
					hDir = "►";
					break;
				default:
					hDir = " ";
					break;
			}
			switch (VerticalDirection)
			{
				case -1:
					vDir = "▲";
					break;
				case 1:
					vDir = "▼";
					break;
				default:
					vDir = " ";
					break;
			}

			return $"X={X},Y={Y} (P={Position}{hDir}{vDir})";
		}
	}
}
