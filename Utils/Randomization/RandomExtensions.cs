using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Randomization
{
    /// <summary>
    /// Provides helper methods to build random strings using <see cref="Random"/>.
    /// </summary>
    public static class RandomExtensions
    {
        private static readonly string defaultRandomCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";

		/// <summary>
		/// Generates a random string of a fixed length.
		/// </summary>
		/// <param name="r">Random generator used to pick characters.</param>
		/// <param name="length">Length of the string to create.</param>
		/// <param name="characters">Optional alphabet to pick characters from.</param>
		/// <returns>A random string composed of the requested number of characters.</returns>
		public static string RandomString(this Random r, int length, string characters = null)
				=> RandomString(r, length, length, (characters ?? defaultRandomCharacters).AsSpan());

		/// <summary>
		/// Generates a random string of a fixed length.
		/// </summary>
		/// <param name="r">Random generator used to pick characters.</param>
		/// <param name="length">Length of the string to create.</param>
		/// <param name="characters">Optional alphabet to pick characters from.</param>
		/// <returns>A random string composed of the requested number of characters.</returns>
		public static string RandomString(this Random r, int length, char[] characters) 
		{
			ArgumentNullException.ThrowIfNullOrWhiteSpace(nameof(characters));
			return RandomString(r, length, length, characters.AsSpan());
		}

        /// <summary>
        /// Generates a random string whose length is between the supplied bounds.
        /// </summary>
        /// <param name="r">Random generator used to pick characters.</param>
        /// <param name="minLength">Inclusive minimum length of the generated string.</param>
        /// <param name="maxLength">Exclusive maximum length of the generated string.</param>
        /// <param name="characters">Optional alphabet to pick characters from.</param>
        /// <returns>A random string composed of characters sampled from <paramref name="characters"/>.</returns>
        public static string RandomString(this Random r, int minLength, int maxLength, string characters = null)
			=> RandomString(r, minLength, maxLength, (characters ?? defaultRandomCharacters).AsSpan());

		/// <summary>
		/// Generates a random string whose length is between the supplied bounds.
		/// </summary>
		/// <param name="r">Random generator used to pick characters.</param>
		/// <param name="minLength">Inclusive minimum length of the generated string.</param>
		/// <param name="maxLength">Exclusive maximum length of the generated string.</param>
		/// <param name="characters">Optional alphabet to pick characters from.</param>
		/// <returns>A random string composed of characters sampled from <paramref name="characters"/>.</returns>
		public static string RandomString(this Random r, int minLength, int maxLength, char[] characters)
        {
			ArgumentNullException.ThrowIfNullOrWhiteSpace(nameof(characters));
			return RandomString(r, minLength, maxLength, characters.AsSpan());
        }

		/// <summary>
		/// Generates a random string whose length is between the supplied bounds.
		/// </summary>
		/// <param name="r">Random generator used to pick characters.</param>
		/// <param name="minLength">Inclusive minimum length of the generated string.</param>
		/// <param name="maxLength">Exclusive maximum length of the generated string.</param>
		/// <param name="characters">Alphabet to pick characters from.</param>
		/// <returns>A random string composed of characters sampled from <paramref name="characters"/>.</returns>
		public static string RandomString(this Random r, int minLength, int maxLength, ReadOnlySpan<char> characters)
        {
			ArgumentNullException.ThrowIfNullOrWhiteSpace(nameof(r));
			var length = r.Next(minLength, maxLength);

            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = characters[r.Next(0, characters.Length - 1)];
            }
            return new string(result);
        }

		/// <summary>
		/// Generates an array with a fixed number of elements using the provided value factory.
		/// </summary>
		/// <typeparam name="T">Type of the elements to produce.</typeparam>
		/// <param name="r">The random generator used for incidental sampling inside <paramref name="value"/>.</param>
		/// <param name="size">Number of elements to generate.</param>
		/// <param name="value">Factory that produces the element at a given index.</param>
		/// <returns>A new array populated by invoking <paramref name="value"/> for each index.</returns>
		public static T[] RandomArray<T>(this Random r, int size, Func<int, T> value)
		{
			T[] result = new T[size];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = value(i);
			}
			return result;
		}

		/// <summary>
		/// Generates an array with a random length within the specified bounds.
		/// </summary>
		/// <typeparam name="T">Type of the elements to produce.</typeparam>
		/// <param name="r">The random generator used for length selection and element production.</param>
		/// <param name="minSize">Inclusive minimum number of elements.</param>
		/// <param name="maxSize">Exclusive maximum number of elements.</param>
		/// <param name="value">Factory that produces the element at a given index.</param>
		/// <returns>A new array of random length populated with values from <paramref name="value"/>.</returns>
		public static T[] RandomArray<T>(this Random r, int minSize, int maxSize, Func<int, T> value)
		{
			T[] result = new T[r.Next(minSize, maxSize)];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = value(i);
			}
			return result;
		}

		/// <summary>
		/// Generates an array of random bytes with the specified length.
		/// </summary>
		/// <param name="r">The random generator that produces the bytes.</param>
		/// <param name="size">The number of bytes to generate.</param>
		/// <returns>An array filled with random bytes.</returns>
		public static byte[] NextBytes(this Random r, int size)
		{
			byte[] result = new byte[size];
			r.NextBytes(result);
			return result;
		}

		/// <summary>
		/// Generates an array of random bytes whose length is chosen randomly between the supplied bounds.
		/// </summary>
		/// <param name="r">The random generator that produces the bytes.</param>
		/// <param name="minSize">Inclusive minimum number of bytes.</param>
		/// <param name="maxSize">Exclusive maximum number of bytes.</param>
		/// <returns>An array filled with random bytes.</returns>
		public static byte[] NextBytes(this Random r, int minSize, int maxSize)
		{
			byte[] result = new byte[r.Next(minSize, maxSize)];
			r.NextBytes(result);
			return result;
		}

		/// <summary>
		/// Generates a random <see cref="byte"/> value using <see cref="Random.NextBytes(byte[])"/>.
		/// </summary>
		/// <param name="r">The random generator.</param>
		/// <returns>A random byte value.</returns>
		public static byte RandomByte(this Random r)
		{
			byte[] result = new byte[sizeof(byte)];
			r.NextBytes(result);
			return result[0];
		}

		/// <summary>
		/// Generates a random <see cref="short"/> value from random bytes.
		/// </summary>
		/// <param name="r">The random generator.</param>
		/// <returns>A random short value.</returns>
		public static short RandomShort(this Random r)
		{
			byte[] result = new byte[sizeof(short)];
			r.NextBytes(result);
			return BitConverter.ToInt16(result, 0);
		}

		/// <summary>
		/// Generates a random <see cref="int"/> value from random bytes.
		/// </summary>
		/// <param name="r">The random generator.</param>
		/// <returns>A random integer value.</returns>
		public static int RandomInt(this Random r)
		{
			byte[] result = new byte[sizeof(int)];
			r.NextBytes(result);
			return BitConverter.ToInt32(result, 0);
		}

		/// <summary>
		/// Generates a random <see cref="long"/> value from random bytes.
		/// </summary>
		/// <param name="r">The random generator.</param>
		/// <returns>A random long value.</returns>
		public static long RandomLong(this Random r)
		{
			byte[] result = new byte[sizeof(long)];
			r.NextBytes(result);
			return BitConverter.ToInt64(result, 0);
		}

		/// <summary>
		/// Generates a random <see cref="ushort"/> value from random bytes.
		/// </summary>
		/// <param name="r">The random generator.</param>
		/// <returns>A random unsigned short value.</returns>
		public static ushort RandomUShort(this Random r)
		{
			byte[] result = new byte[sizeof(ushort)];
			r.NextBytes(result);
			return BitConverter.ToUInt16(result, 0);
		}

		/// <summary>
		/// Generates a random <see cref="uint"/> value from random bytes.
		/// </summary>
		/// <param name="r">The random generator.</param>
		/// <returns>A random unsigned integer value.</returns>
		public static uint RandomUInt(this Random r)
		{
			byte[] result = new byte[sizeof(uint)];
			r.NextBytes(result);
			return BitConverter.ToUInt32(result, 0);
		}

		/// <summary>
		/// Generates a random <see cref="ulong"/> value from random bytes.
		/// </summary>
		/// <param name="r">The random generator.</param>
		/// <returns>A random unsigned long value.</returns>
		public static ulong RandomULong(this Random r)
		{
			byte[] result = new byte[sizeof(ulong)];
			r.NextBytes(result);
			return BitConverter.ToUInt64(result, 0);
		}

		/// <summary>
		/// Generates a random <see cref="float"/> value from random bytes.
		/// </summary>
		/// <param name="r">The random generator.</param>
		/// <returns>A random single-precision floating-point value.</returns>
		public static float RandomFloat(this Random r)
		{
			byte[] result = new byte[sizeof(float)];
			r.NextBytes(result);
			return BitConverter.ToSingle(result, 0);
		}

		/// <summary>
		/// Generates a random <see cref="double"/> value from random bytes.
		/// </summary>
		/// <param name="r">The random generator.</param>
		/// <returns>A random double-precision floating-point value.</returns>
		public static double RandomDouble(this Random r)
		{
			byte[] result = new byte[sizeof(double)];
			r.NextBytes(result);
			return BitConverter.ToDouble(result, 0);
		}

		/// <summary>
		/// Selects a random element from the provided parameter array.
		/// </summary>
		/// <typeparam name="T">Type of the elements to select.</typeparam>
		/// <param name="r">The random generator.</param>
		/// <param name="values">The set of values to pick from.</param>
		/// <returns>A randomly chosen element.</returns>
		public static T RandomFrom<T>(this Random r, params T[] values)
				=> values[r.Next(values.Length)];

		/// <summary>
		/// Selects a random element from the provided span.
		/// </summary>
		/// <typeparam name="T">Type of the elements to select.</typeparam>
		/// <param name="r">The random generator.</param>
		/// <param name="values">The span of values to pick from.</param>
		/// <returns>A randomly chosen element.</returns>
		public static T RandomFrom<T>(this Random r, Span<T> values)
				=> values[r.Next(values.Length)];

		/// <summary>
		/// Selects a random element from the provided read-only list.
		/// </summary>
		/// <typeparam name="T">Type of the elements to select.</typeparam>
		/// <param name="r">The random generator.</param>
		/// <param name="values">The list of values to pick from.</param>
		/// <returns>A randomly chosen element.</returns>
		public static T RandomFrom<T>(this Random r, IReadOnlyList<T> values)
				=> values[r.Next(values.Count)];

	}
}
