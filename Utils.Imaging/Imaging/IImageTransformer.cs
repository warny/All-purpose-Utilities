using System.Drawing;
using System.Numerics;

namespace Utils.Imaging
{
    /// <summary>
    /// Defines a generic transformation that can be applied to an image.
    /// </summary>
    /// <typeparam name="A">Color type.</typeparam>
    /// <typeparam name="T">Component type.</typeparam>
    public interface IImageTransformer<A, T>
        where A : struct, IColorArgb<T>
        where T : struct, INumber<T>
    {
        /// <summary>
        /// Transforms the specified image in place.
        /// </summary>
        /// <param name="accessor">Image to transform.</param>
        void Transform(IImageAccessor<A, T> accessor);

        /// <summary>
        /// Transforms the specified image in place using a per-pixel mask.
        /// </summary>
        /// <param name="accessor">Image to transform.</param>
        /// <param name="mask">Mask controlling the influence of the transformation.</param>
        void Transform(IImageAccessor<A, T> accessor, IImageAccessor<A, T> mask);
    }
}
