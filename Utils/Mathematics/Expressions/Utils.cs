using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions.ExpressionBuilders;

namespace Utils.Expressions
{
    public static class Utils
    {
        public static IEnumerable<string> SplitCommaSeparatedList(string commaSeparatedValues, char commaChar, params Markers[] depthMarkerChars)
            => SplitCommaSeparatedList(commaSeparatedValues, commaChar, false, depthMarkerChars);

        public static IEnumerable<string> SplitCommaSeparatedList(string commaSeparatedValues, char commaChar, bool removeEmptyEntries, params Markers[] depthMarkerChars)
        {
            var lastTypeIndex = 0;
            var depth = new Stack<Markers>();
            for (int i = 0; i < commaSeparatedValues.Length; i++)
            {
                char current = commaSeparatedValues[i];
                Markers m;
                if ((m = depthMarkerChars.FirstOrDefault(m => m.Start == current)) != null)
                {
                    if (depth.Any() && m.Start == m.End)
                    {
                        depth.Pop();
                        continue;
                    }

                    depth.Push(m);
                    continue;
                }
                if ((m = depthMarkerChars.FirstOrDefault(m => m.End == current)) != null)
                {
                    var startChar = depth.Pop();
                    if (startChar.End == current) continue;
                    throw new Exception(commaSeparatedValues);
                }
                if (current == commaChar && !depth.Any())
                {
                    var value = commaSeparatedValues[lastTypeIndex..i];
                    if (!removeEmptyEntries || !string.IsNullOrEmpty(value)) yield return value;
                    lastTypeIndex = i + 1;
                }
            }
            {
                var value = commaSeparatedValues[lastTypeIndex..];
                if (!removeEmptyEntries || !string.IsNullOrEmpty(value)) yield return value;
            }
        }
    }

    public record Markers(char Start, char End)
    {
        public static implicit operator Markers((char Start, char End) value) => new Markers(value.Start, value.End);
    }

    public record WrapMarkers(string Start, string End, string Separator)
    {
        public bool Test(string token, out bool isEnd)
        {
            isEnd = false;
            if (token == End) { isEnd = true; return true; }
            if (token == Separator) return true;
            return false;
        }
        public bool Test(string token, bool ignoreSeparator, out bool isEnd)
        {
            isEnd = false;
            if (token == End) { isEnd = true; return true; }
            if (ignoreSeparator || token == Separator) return true;
            return false;
        }
    }
}

