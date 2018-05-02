using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utils.Imaging;

namespace UtilsForm
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load( object sender, EventArgs e )
		{
			var image = Resources.Jellyfish;
			Utils.Imaging.Processing.Contour contour = new Utils.Imaging.Processing.Contour();

			using (var accessor = new BitmapArgb32Accessor(image)) {
				Utils.Imaging.Processing.ImageProcessorArgb32.Process(accessor, contour);
			}
			this.BackgroundImage = image;
		}
	}
}
