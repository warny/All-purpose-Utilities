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
    public partial class FactalsForm : Form
    {
        IComputeFractal computeFractalMandelbrot = null;
        IComputeFractal computeFractalJulia = null;

        public FactalsForm()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void mnuQuit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void PrepareFractal()
        {
            if (this.WindowState == FormWindowState.Minimized) return;

            this.InitFractal(ref this.computeFractalMandelbrot, DisplayMandelbrot, (bitmap) => new ComputeFractal<Mandelbrot>(bitmap, new Complex(-1, 0), new Complex(0, 0), 0.002));
            this.InitFractal(ref this.computeFractalJulia, DisplayJulia, (bitmap) => new ComputeFractal<Julia>(bitmap, new Complex(0, 0), new Complex(-1.417022285618, 0.0099534), 0.000001));
            //this.InitFractal(ref this.computeFractalJulia, DisplayJulia, (bitmap) => new ComputeFractal<Julia>(bitmap, new Complex(0, 0), new Complex(-1, 0.1), 0.002));
        }

        private void InitFractal(ref IComputeFractal computeFractal, PictureBox display, Func<Bitmap, IComputeFractal> InitFractal)
        {
            if (computeFractal is null || display.Image is null || computeFractal.Image.Width != display.Width || computeFractal.Image.Height != display.Height)
            {
                Bitmap bitmap = new Bitmap(DisplayJulia.Width, DisplayJulia.Height);
                DisplayJulia.Image = bitmap;
                computeFractal = InitFractal(bitmap);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            PrepareFractal();
            computeFractalMandelbrot?.Compute();
            DisplayMandelbrot.Image = computeFractalMandelbrot?.Image;
            computeFractalJulia?.Compute();
            DisplayJulia.Image = computeFractalJulia?.Image;
        }
    }
}
