using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Utils.Mathematics.Fourrier;

public class FastFourrierTransform
{
        /// <summary>
        /// Separates even and odd elements so the even values occupy the first half of the array range and the odd values occupy the second half.
        /// </summary>
        /// <param name="array">Array to reorder.</param>
        /// <param name="start">Inclusive start index.</param>
        /// <param name="end">Exclusive end index.</param>

        private void Separate(Complex[] array, int start, int end)
        {
                int n = end-start;
		Complex[] buffer = new Complex[n/2];  // get temp heap storage
		for (int i = 0 ; i<n/2 ; i++)    // copy all odd elements to heap storage
			buffer[i] = array[i*2+1];
		for (int i = 0 ; i<n/2 ; i++)    // copy all even elements to lower-half of a[]
			array[i] = array[i*2];
		for (int i = 0 ; i<n/2 ; i++)    // copy all odd (from heap) to upper-half of a[]
			array[i+n/2] = buffer[i];
	}

        /// <summary>
        /// Performs an in-place FFT on the entire sample array.
        /// </summary>
        /// <param name="array">Array to transform.</param>
        public void Transform(Complex[] array)
        {
                Transform(array, 0, array.Length);
        }

	// N must be a power-of-2, or bad things will happen.
	// Currently no check for this condition.
	//
	// N input samples in X[] are FFT'd and results left in X[].
	// Because of Nyquist theorem, N samples means 
	// only first N/2 FFT results in X[] are the answer.
	// (upper half of X[] is a reflection with no new information).
        /// <summary>
        /// Performs an in-place FFT on a range of the provided array.
        /// </summary>
        /// <param name="array">Array to transform.</param>
        /// <param name="start">Inclusive start index.</param>
        /// <param name="end">Exclusive end index.</param>
        public void Transform(Complex[] array, int start, int end)
        {
                int N = end - start;
                if (N < 2) {
                        return;
                }

                Separate(array, start, end);      // all evens to lower half, all odds to upper half
                Transform(array, start, start + N/2);   // recurse even items
                Transform(array, start + N/2, end);   // recurse odd  items
												  // combine results of two half recursions
		for (int k = 0 ; k<N/2 ; k++) {
			Complex e = array[k];   // even
			Complex o = array[k+N/2];   // odd
										// w is the "twiddle-factor"
			Complex w = Complex.Exp(new Complex(0, -2 * Math.PI * k / N));
			array[k] = e + w * o;
			array[k+N/2] = e - w * o;
		}
	}
}
