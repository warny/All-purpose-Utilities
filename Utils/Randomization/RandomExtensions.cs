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
        private static readonly char[] defaultRandomCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ".ToCharArray();

        /// <summary>
        /// Generates a random string of a fixed length.
        /// </summary>
        /// <param name="r">Random generator used to pick characters.</param>
        /// <param name="length">Length of the string to create.</param>
        /// <param name="characters">Optional alphabet to pick characters from.</param>
        /// <returns>A random string composed of the requested number of characters.</returns>
        public static string RandomString(this Random r, int length, char[] characters = null)
                => RandomString(r, length, length, characters);

        /// <summary>
        /// Generates a random string whose length is between the supplied bounds.
        /// </summary>
        /// <param name="r">Random generator used to pick characters.</param>
        /// <param name="minLength">Inclusive minimum length of the generated string.</param>
        /// <param name="maxLength">Exclusive maximum length of the generated string.</param>
        /// <param name="characters">Optional alphabet to pick characters from.</param>
        /// <returns>A random string composed of characters sampled from <paramref name="characters"/>.</returns>
        public static string RandomString(this Random r, int minLength, int maxLength, char[] characters = null)
        {
            r.Arg().MustNotBeNull();
            characters ??= defaultRandomCharacters;
            var length = r.Next(minLength, maxLength);

            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = characters[r.Next(0, characters.Length - 1)];
            }
            return new string(result);
        }


    }
}
