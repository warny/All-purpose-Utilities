using System.Drawing;

namespace Fractals
{
    public interface IComputeFractal
    {
        Bitmap Image { get; }

        void Compute();
    }
}