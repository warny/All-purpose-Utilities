using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

public static class ExpressionExtensions
{
    private static readonly ExpressionSimplifier simplifier = new ExpressionSimplifier();

    public static Expression Simplify(this Expression e)
    {
        return simplifier.Simplify(e);
    }

}

public class ExpressionExtensionsException : Exception
{
    public ExpressionExtensionsException(string msg) : base(msg, null) { }
    public ExpressionExtensionsException(string msg, Exception innerException) : base(msg, innerException) { }
}
