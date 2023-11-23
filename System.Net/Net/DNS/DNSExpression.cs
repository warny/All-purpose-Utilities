﻿using System;
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
            if (element is ParameterExpression pe)
            {
                return ExpressionParser.ParseExpression(expression, [pe], null, true, ["Utils.Net.DNS"]);
            }
            else
            {
                var variable = Expression.Variable(element.Type, "<default>");
                return Expression.Block([variable],
                    Expression.Assign(variable, element),
                    ExpressionParser.ParseExpression(expression, [variable], null, true, ["Utils.Net.DNS"])
                );
            }
        }
    }
}
