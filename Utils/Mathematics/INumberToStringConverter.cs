using System.Collections.Generic;
using System.Numerics;

namespace Utils.Mathematics
{
        public interface INumberToStringConverter
        {
                /// <summary>
                /// Maximum number that can be reliably converted or null when unlimited.
                /// </summary>
                BigInteger? MaxNumber { get; }
                string Convert(BigInteger number);
                string Convert(int number);
                string Convert(long number);
                string Convert(decimal number);
        }
}