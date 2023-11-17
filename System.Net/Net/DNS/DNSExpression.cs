using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Utils.Expressions;

namespace Utils.Net.DNS
{
    internal static class DNSExpression
    {
        public static Expression BuildExpression(Expression element, string expression)
        {
            var result = ExpressionParser.Parse(expression, element.Type, ["Utils.Net.DNS"]);
            var result2 = ExpressionEx.ExtractInnerExpression(result, element);
            return result2;
        }
    }
}
