using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utils.Drawing;
using Utils.Imaging;

namespace DrawTest
{
	public partial class TestForm : Form
	{
		public TestForm()
		{
			InitializeComponent();
		}

		private void pictureBox1_Paint(object sender, PaintEventArgs e)
		{
			Bitmap image = new Bitmap(pictureBox1.Width, pictureBox1.Height);
			using (var a = new BitmapArgb32Accessor(image))
			{
				var d = new Draw<ColorArgb32>(a);
				d.FillRectangle(
					new Point(0, 0),
					new Point(500, 500),
					new ColorArgb32(0, 0, 50)
				);
				d.FillBezier(new ColorArgb32(0, 255, 0),
					new Point(0, 0),
					new Point(500, 0),
					new Point(500, 500),
					new Point(0, 500),
					new Point(0, 0)
				);
				d.DrawLine(new Point(200, 0), new Point(0, 200), new ColorArgb32(255, 0, 0));
				d.DrawLine(new Point(100, 0), new Point(0, 200), new ColorArgb32(0, 255, 0));
				d.DrawLine(new Point(200, 0), new Point(0, 100), new ColorArgb32(0, 0, 255));
				d.DrawLine(new Point(-200, -200), new Point(200, 200), new ColorArgb32(255, 0, 0));
				d.DrawLine(new Point(-200, -200), new Point(400, 200), new ColorArgb32(255, 0, 0));
				d.DrawLine(new Point(-200, -200), new Point(200, 400), new ColorArgb32(255, 0, 0));
				d.DrawBezier(new ColorArgb32(255, 0, 0),
					new Point(0, 0),
					new Point(500, 0),
					new Point(500, 500),
					new Point(0, 500)
				);
				d.DrawBezier(new ColorArgb32(255, 0, 0),
					new Point(0, 0),
					new Point(250, 0),
					new Point(250, 500),
					new Point(0, 500)
				);
				d.FillCircle(new Point (1000, 300), 250, new ColorArgb32(255, 255, 0));
				d.DrawEllipse(new Point(1000, 300), 250, 100, new ColorArgb32(0, 255, 255), 0);
				d.FillCircle(new Point(1000, 300), 250, new ColorArgb32(0, 0, 0), Math.PI / 2, Math.PI);
				Func<float, ColorArgb32> c1 = g => ColorArgb32.LinearGrandient(new ColorArgb32(255, 0, 255), new ColorArgb32(255, 255, 0), g);
				d.DrawCircle(new Point(1000, 300), 250, c1, Math.PI / 2, Math.PI) ;

			}
			pictureBox1.Image = image;
		}
	}
}
