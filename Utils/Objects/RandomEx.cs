using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Objects
{
    public static class RandomEx
    {
        public static T[] RandomArray<T>(this Random r, int minSize, int maxSize, Func<int, T> value)
        {
            T[] result = new T[r.Next(minSize, maxSize)];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = value(i);
            }
            return result;
        }

        public static byte[] NextBytes(this Random r, int size)
        {
            byte[] result = new byte[size];
            r.NextBytes(result);
            return result;
        }

        public static byte[] NextBytes(this Random r, int minSize, int maxSize)
        {
            byte[] result = new byte[r.Next(minSize, maxSize)];
            r.NextBytes(result);
            return result;
        }


    }
}
