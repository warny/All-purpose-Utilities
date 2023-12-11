using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static System.Reflection.BindingFlags;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;
using System.Text.RegularExpressions;
using Utils.Reflection;

namespace Utils.Expressions
{
    public abstract class ExpressionTranformer
    {
        private static Type TypeOfExpression = typeof(Expression);
        protected virtual Expression PrepareExpression(Expression e) => e;

        protected Expression Transform(Expression e)
        {
            Type t = GetType();

            object[] parameters = null;
            Expression[] expressionParameters = null;

            switch (e)
            {
                case ConstantExpression cc:
                    expressionParameters = new Expression[0];
                    parameters = [cc, cc.Value];
                    break;
                case UnaryExpression ue:
                    expressionParameters = new Expression[] { PrepareExpression(ue.Operand) };
                    e = ue = (UnaryExpression)CopyExpression(e, expressionParameters);
                    parameters = [ue, ue.Operand];
                    break;
                case BinaryExpression be:
                    expressionParameters = new Expression[] { PrepareExpression(be.Left), PrepareExpression(be.Right) };
                    e = be = (BinaryExpression)CopyExpression(e, expressionParameters);
                    parameters = [be, be.Left, be.Right];
                    break;
                case MethodCallExpression mce:
                    {
                        expressionParameters = mce.Arguments.Select(a => PrepareExpression(a)).ToArray();
                        e = mce = (MethodCallExpression)CopyExpression(e, expressionParameters);
                        parameters = new object[mce.Arguments.Count + 1];
                        parameters[0] = mce;
                        Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
                        break;
                    }

                case ParameterExpression pe:
                    expressionParameters = new Expression[0];
                    parameters = [pe];
                    break;
                case InvocationExpression ie:
                    {
                        expressionParameters = ie.Arguments.Select(a => PrepareExpression(a)).ToArray();
                        e = ie = (InvocationExpression)CopyExpression(e, expressionParameters);
                        parameters = new object[ie.Arguments.Count + 1];
                        parameters[0] = ie;
                        Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
                        break;
                    }

                case LambdaExpression le:
                    {
                        expressionParameters = le.Parameters.Select(a => (ParameterExpression)PrepareExpression(a)).ToArray();
                        e = le = Expression.Lambda(Transform(le.Body), (ParameterExpression[])expressionParameters);
                        parameters = new object[le.Parameters.Count + 1];
                        parameters[0] = le;
                        Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
                        break;
                    }

                default:
                    expressionParameters = new Expression[] { };
                    parameters = new[] { e };
                    break;
            }

            foreach (var method in t.GetMethods(Public | NonPublic | InvokeMethod | Instance))
            {
                var attr = method.GetCustomAttributes<ExpressionSignatureAttribute>().FirstOrDefault();
                if (attr is null) continue;
                if (!attr.Match(e)) continue;
                if (!TypeOfExpression.IsAssignableFromEx(method.ReturnType)) throw new InvalidProgramException();

                var parametersInfo = method.GetParameters();

                if (!parametersInfo[0].ParameterType.IsInstanceOfType(parameters[0])) continue;
                object result;
                if (parametersInfo.Length > 1)
                {
                    if (parametersInfo[1].ParameterType == typeof(Expression[]))
                    {
                        result = method.Invoke(this, [e, expressionParameters]);
                    }
                    else
                    {
                        bool isValid = true;
                        for (int i = 1; i < parametersInfo.Length; i++)
                        {
                            if (parameters[i] is Expression pe)
                            {
                                if (!CheckParameter(pe, parametersInfo[i]))
                                {
                                    isValid = false;
                                    break;
                                }
                            }
                            else
                            {
                                if (!parametersInfo[1].ParameterType.IsAssignableFrom(e.Type))
                                {
                                    isValid = false;
                                    break;
                                }
                            }
                        }
                        if (!isValid) continue;
                        result = method.Invoke(this, parameters);
                        if (result is null) continue;
                    }
                }
                else if (parametersInfo.Length == 1)
                {
                    result = method.Invoke(this, new[] { parameters[0] });
                }
                else
                {
                    continue;
                }
                return (Expression)result;
            }

            {
                if (e is ConstantExpression cc)
                {
                    return FinalizeExpression(e, new Expression[0]);
                }
                else
                {
                    return FinalizeExpression(e, expressionParameters);
                }

            }
        }

        protected virtual Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            throw new Exception("La transformation de l'expression ne peut être finalisée");
        }

        protected Expression ReplaceArguments(Expression e, ParameterExpression[] oldParameters, Expression[] newParameters)
        {
            switch (e)
            {
                case ParameterExpression pe:
                    {
                        int i = Array.IndexOf(oldParameters, pe);
                        return newParameters[i];
                    }

                case UnaryExpression ue:
                    return CopyExpression(ue, ReplaceArguments(ue.Operand, oldParameters, newParameters));
                case BinaryExpression be:
                    return CopyExpression(be, ReplaceArguments(be.Left, oldParameters, newParameters), ReplaceArguments(be.Right, oldParameters, newParameters));
                case InvocationExpression ie:
                    {
                        var arguments = ie.Arguments.Select(a => ReplaceArguments(a, oldParameters, newParameters)).ToArray();
                        return CopyExpression(ie, arguments);
                    }

                case MethodCallExpression mce:
                    {
                        var arguments = mce.Arguments.Select(a => ReplaceArguments(a, oldParameters, newParameters)).ToArray();
                        return CopyExpression(mce, arguments);
                    }
            }
            return e;
        }

        protected Expression CopyExpression(Expression e, params Expression[] parameters)
        {
            return e.NodeType switch
            {
                ExpressionType.Add => Expression.Add(parameters[0], parameters[1]),
                ExpressionType.AddChecked => Expression.AddChecked(parameters[0], parameters[1]),
                ExpressionType.And => Expression.And(parameters[0], parameters[1]),
                ExpressionType.AndAlso => Expression.AndAlso(parameters[0], parameters[1]),
                ExpressionType.ArrayLength => Expression.ArrayLength(parameters[0]),
                ExpressionType.ArrayIndex => Expression.ArrayIndex(parameters[0], parameters[1]),
                ExpressionType.Call => Expression.Call(((MethodCallExpression)e).Method, parameters),
                ExpressionType.Coalesce => Expression.Coalesce(parameters[0], parameters[1]),
                ExpressionType.Conditional => Expression.Condition(parameters[0], parameters[1], parameters[2]),
                ExpressionType.Constant => Expression.Constant(((ConstantExpression)e).Value, e.Type),
                ExpressionType.Convert => Expression.Convert(parameters[0], ((UnaryExpression)e).Type),
                ExpressionType.ConvertChecked => Expression.ConvertChecked(parameters[0], ((UnaryExpression)e).Type),
                ExpressionType.Divide => Expression.Divide(parameters[0], parameters[1]),
                ExpressionType.Equal => Expression.Equal(parameters[0], parameters[1]),
                ExpressionType.ExclusiveOr => Expression.ExclusiveOr(parameters[0], parameters[1]),
                ExpressionType.GreaterThan => Expression.GreaterThan(parameters[0], parameters[1]),
                ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(parameters[0], parameters[1]),
                ExpressionType.Invoke => Expression.Invoke(((InvocationExpression)e).Expression, parameters),
                ExpressionType.Lambda => e,
                ExpressionType.LeftShift => Expression.LeftShift(parameters[0], parameters[1]),
                ExpressionType.LessThan => Expression.LessThan(parameters[0], parameters[1]),
                ExpressionType.LessThanOrEqual => Expression.LessThanOrEqual(parameters[0], parameters[1]),
                ExpressionType.ListInit => e,
                ExpressionType.MemberAccess => e,
                ExpressionType.MemberInit => e,
                ExpressionType.Modulo => Expression.Modulo(parameters[0], parameters[1]),
                ExpressionType.Multiply => Expression.Multiply(parameters[0], parameters[1]),
                ExpressionType.MultiplyChecked => Expression.MultiplyChecked(parameters[0], parameters[1]),
                ExpressionType.Negate => Expression.Negate(parameters[0]),
                ExpressionType.UnaryPlus => Expression.UnaryPlus(parameters[0]),
                ExpressionType.NegateChecked => Expression.NegateChecked(parameters[0]),
                ExpressionType.New => Expression.New(((NewExpression)e).Constructor, parameters),
                ExpressionType.NewArrayInit => Expression.NewArrayInit(((NewArrayExpression)e).Type, parameters),
                ExpressionType.NewArrayBounds => Expression.NewArrayBounds(((NewArrayExpression)e).Type, parameters),
                ExpressionType.Not => Expression.Not(parameters[0]),
                ExpressionType.NotEqual => Expression.NotEqual(parameters[0], parameters[1]),
                ExpressionType.Or => Expression.Or(parameters[0], parameters[1]),
                ExpressionType.OrElse => Expression.OrElse(parameters[0], parameters[1]),
                ExpressionType.Parameter => e,
                ExpressionType.Power => Expression.Power(parameters[0], parameters[1]),
                ExpressionType.Quote => Expression.Quote(parameters[0]),
                ExpressionType.RightShift => Expression.RightShift(parameters[0], parameters[1]),
                ExpressionType.Subtract => Expression.Subtract(parameters[0], parameters[1]),
                ExpressionType.SubtractChecked => Expression.SubtractChecked(parameters[0], parameters[1]),
                ExpressionType.TypeAs => Expression.TypeAs(parameters[0], ((UnaryExpression)e).Type),
                ExpressionType.TypeIs => Expression.TypeIs(parameters[0], ((UnaryExpression)e).Type),
                ExpressionType.Assign => Expression.Assign(parameters[0], parameters[1]),
                ExpressionType.Block => Expression.Block(parameters),
                ExpressionType.DebugInfo => e,
                ExpressionType.Decrement => Expression.Decrement(parameters[0]),
                ExpressionType.Dynamic => e,
                ExpressionType.Default => e,
                ExpressionType.Extension => e,
                ExpressionType.Goto => e,
                ExpressionType.Increment => Expression.Increment(parameters[0]),
                ExpressionType.Index => e,
                ExpressionType.Label => e,
                ExpressionType.RuntimeVariables => e,
                ExpressionType.Loop => Expression.Loop(parameters[0]),
                ExpressionType.Switch => e,
                ExpressionType.Throw => Expression.Throw(parameters[0]),
                ExpressionType.Try => e,
                ExpressionType.Unbox => Expression.Unbox(parameters[0], ((UnaryExpression)e).Type),
                ExpressionType.AddAssign => Expression.AddAssign(parameters[0], parameters[1]),
                ExpressionType.AndAssign => Expression.AndAssign(parameters[0], parameters[1]),
                ExpressionType.DivideAssign => Expression.DivideAssign(parameters[0], parameters[1]),
                ExpressionType.ExclusiveOrAssign => Expression.ExclusiveOrAssign(parameters[0], parameters[1]),
                ExpressionType.LeftShiftAssign => Expression.LeftShiftAssign(parameters[0], parameters[1]),
                ExpressionType.ModuloAssign => Expression.ModuloAssign(parameters[0], parameters[1]),
                ExpressionType.MultiplyAssign => Expression.MultiplyAssign(parameters[0], parameters[1]),
                ExpressionType.OrAssign => Expression.OrAssign(parameters[0], parameters[1]),
                ExpressionType.PowerAssign => Expression.PowerAssign(parameters[0], parameters[1]),
                ExpressionType.RightShiftAssign => Expression.RightShiftAssign(parameters[0], parameters[1]),
                ExpressionType.SubtractAssign => Expression.SubtractAssign(parameters[0], parameters[1]),
                ExpressionType.AddAssignChecked => Expression.AddAssignChecked(parameters[0], parameters[1]),
                ExpressionType.MultiplyAssignChecked => Expression.MultiplyAssignChecked(parameters[0], parameters[1]),
                ExpressionType.SubtractAssignChecked => Expression.SubtractAssignChecked(parameters[0], parameters[1]),
                ExpressionType.PreIncrementAssign => Expression.PreIncrementAssign(parameters[0]),
                ExpressionType.PreDecrementAssign => Expression.PreDecrementAssign(parameters[0]),
                ExpressionType.PostIncrementAssign => Expression.PostIncrementAssign(parameters[0]),
                ExpressionType.PostDecrementAssign => Expression.PostDecrementAssign(parameters[0]),
                ExpressionType.TypeEqual => Expression.SubtractAssignChecked(parameters[0], parameters[1]),
                ExpressionType.OnesComplement => Expression.OnesComplement(parameters[0]),
                ExpressionType.IsTrue => Expression.IsTrue(parameters[0]),
                ExpressionType.IsFalse => Expression.IsFalse(parameters[0]),
                _ => throw new Exception("L'expression ne peut pas être copiée"),
            };
        }


        private bool CheckParameter(Expression e, ParameterInfo parameter)
        {
            if (!parameter.ParameterType.IsAssignableFrom(e.GetType())) return false;
            var attribute = parameter.GetCustomAttributes<ExpressionSignatureAttribute>().FirstOrDefault();
            if (attribute is null) return true;
            return attribute.Match(e);
        }


    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class ExpressionSignatureAttribute(ExpressionType expressionType) : Attribute
    {
        public ExpressionType ExpressionType => expressionType;

        public virtual bool Match(Expression e)
        {
            return ExpressionType == (ExpressionType)(-1) || e.NodeType == ExpressionType;
        }
    }

    public class ExpressionCallSignatureAttribute(Type type, string functionName) : ExpressionSignatureAttribute(ExpressionType.Call)
    {
        public Type Type => type;
        public string FunctionName => functionName;

        public override bool Match(Expression e)
        {
            var ec = e as MethodCallExpression;
            return ec is not null && ec.Method.DeclaringType.IsDefinedBy(Type) && ec.Method.Name == FunctionName;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class ConstantNumericAttribute : ExpressionSignatureAttribute
    {
        public double[] Values { get; }

        public ConstantNumericAttribute() : base(ExpressionType.Constant)
        {
            Values = null;
        }

        public ConstantNumericAttribute(params double[] values) : base(ExpressionType.Constant)
        {
            Values = values;
        }

        public override bool Match(Expression e)
        {
            var cc = e as ConstantExpression;
            if (!(cc is not null && NumberUtils.IsNumeric(cc.Value))) return false;
            if (Values is null) return true;
            return Values.Any(v => v == (double)cc.Value);
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class ReturnTypeAttribute(Type returnType) : ExpressionSignatureAttribute((ExpressionType)(-1))
    {
        public Type ReturnType = returnType;
        public override bool Match(Expression e) => ReturnType.IsAssignableFrom(e.Type);
    }



}
