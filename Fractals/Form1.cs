using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utils.Imaging;

namespace Fractals
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			this.Cursor = Cursors.WaitCursor;

			ComputeFractal computeFractal = new ComputeFractal();
			computeFractal.Compute(Display, new Complex(0, 0), 0.000001, 255, (x, c)=> x * x + c, new Complex( -1.417022285618, 0.0099534));
			//computeFractal.Compute(Display, new Complex(0, 0), 0.002, 255, (x, c) => x * x + c, new Complex(-1, 0.1));
			this.Cursor = Cursors.Default;
		}

		private void mnuQuit_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		private void Display_Paint(object sender, PaintEventArgs e)
		{
		}

		private void Display_Click(object sender, EventArgs e)
		{

		}
	}
}
