using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static System.Reflection.BindingFlags;
using System.Text;
using System.Threading.Tasks;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
	public abstract class ExpressionTranformer
	{
		private static Type TypeOfExpression = typeof(Expression);
		protected virtual Expression PrepareExpression( Expression e ) => e;

		protected Expression Transform( Expression e )
		{
			Type t = this.GetType();

			var cc = e as ConstantExpression;
			var ue = e as UnaryExpression;
			var be = e as BinaryExpression;
			var ie = e as InvocationExpression;
			var le = e as LambdaExpression;
			var mce = e as MethodCallExpression;
			var pe = e as ParameterExpression;

			object[] parameters = null;
			Expression[] expressionParameters = null;

			if (cc!= null) {
				expressionParameters = new Expression[0];
				parameters = new[] { cc, cc.Value };
			} else if (ue != null) {
				expressionParameters = new Expression[] { PrepareExpression(ue.Operand) };
				e = ue = (UnaryExpression)CopyExpression(e, expressionParameters);
				parameters = new[] { ue, ue.Operand };
			} else if (be != null) {
				expressionParameters = new Expression[] { PrepareExpression(be.Left), PrepareExpression(be.Right) };
				e = be = (BinaryExpression)CopyExpression(e, expressionParameters);
				parameters = new[] { be, be.Left, be.Right };
			} else if (mce!= null) {
				expressionParameters = mce.Arguments.Select(a => PrepareExpression(a)).ToArray();
				e = mce = (MethodCallExpression)CopyExpression(e, expressionParameters);
				parameters = new object[mce.Arguments.Count + 1];
				parameters[0] = mce;
				Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
			} else if (pe != null) {
				expressionParameters = new Expression[0];
				parameters = new object[] { pe };
			} else if (ie != null) {
				expressionParameters = ie.Arguments.Select(a => PrepareExpression(a)).ToArray();
				e = ie = (InvocationExpression)CopyExpression(e, expressionParameters);
				parameters = new object[ie.Arguments.Count + 1];
				parameters[0] = ie;
				Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
			} else if (le != null) {
				expressionParameters = le.Parameters.Select(a => (ParameterExpression)PrepareExpression(a)).ToArray();
				e = le = Expression.Lambda(Transform(le.Body), (ParameterExpression[])expressionParameters);
				parameters = new object[le.Parameters.Count + 1];
				parameters[0] = le;
				Array.Copy(expressionParameters, 0, parameters, 1, expressionParameters.Length);
			} else {
				expressionParameters = new Expression[] { };
				parameters = new[] { e };
			}

			foreach (var method in t.GetMethods(Public | NonPublic | InvokeMethod | Instance)) {
				var attr = method.GetCustomAttributes<ExpressionSignatureAttribute>().FirstOrDefault();
				if (attr == null) continue;
				if (!attr.Match(e)) continue;
				if (!TypeOfExpression.IsAssignableFrom(method.ReturnType)) throw new InvalidProgramException();

				var parametersInfo = method.GetParameters();

				if (!parametersInfo[0].ParameterType.IsAssignableFrom(parameters[0].GetType())) continue;
				object result;
				if (parametersInfo.Length > 1) {
					bool isValid = true;
					for (int i = 1 ; i < parametersInfo.Length ; i++) {
						if (parameters[i] is Expression) {
							if (!CheckParameter((Expression)parameters[i], parametersInfo[i])) {
								isValid = false;
								break;
							}
						} else {
							if (!parametersInfo[1].ParameterType.IsAssignableFrom(cc.Type)) {
								isValid = false;
								break;
							}
						}
					}
					if (!isValid) continue;
					result = method.Invoke(this, parameters);
					if (result == null) continue;
				} else if (parametersInfo.Length == 1) {
					result = method.Invoke(this, new[] { parameters[0] });
				} else {
					continue;
				}
				return (Expression)result;
			}

			{
				if (cc!= null) {
					return FinalizeExpression(e, new Expression[0]);
				} else  {
					return FinalizeExpression(e, expressionParameters);
				}

			}
		}

		protected virtual Expression FinalizeExpression( Expression e, Expression[] parameters )
		{
			throw new Exception("La transformation de l'expression ne peut être finalisée");
		}

		protected Expression ReplaceArguments( Expression e, ParameterExpression[] oldParameters, Expression[] newParameters )
		{
			var cc = e as ConstantExpression;
			var ue = e as UnaryExpression;
			var be = e as BinaryExpression;
			var ie = e as InvocationExpression;
			var mce = e as MethodCallExpression;
			var pe = e as ParameterExpression;

			if (pe!=null) {
				int i = Array.IndexOf(oldParameters, pe);
				return newParameters[i];
			} else if (ue!=null) {
				return CopyExpression(ue, ReplaceArguments(ue.Operand, oldParameters, newParameters));
			} else if (be!=null) {
				return CopyExpression(be, ReplaceArguments(be.Left, oldParameters, newParameters), ReplaceArguments(be.Right, oldParameters, newParameters));
			} else if (ie != null) {
				var arguments = ie.Arguments.Select(a => ReplaceArguments(a, oldParameters, newParameters)).ToArray();
				return CopyExpression(ie, arguments);
			} else if (mce !=null) {
				var arguments = mce.Arguments.Select(a => ReplaceArguments(a, oldParameters, newParameters)).ToArray();
				return CopyExpression(mce, arguments);
			}
			return e;
		}

		protected Expression CopyExpression( Expression e, params Expression[] parameters )
		{
			switch (e.NodeType) {
				case ExpressionType.Add:
					return Expression.Add(parameters[0], parameters[1]);
				case ExpressionType.AddChecked:
					return Expression.AddChecked(parameters[0], parameters[1]);
				case ExpressionType.And:
					return Expression.And(parameters[0], parameters[1]);
				case ExpressionType.AndAlso:
					return Expression.AndAlso(parameters[0], parameters[1]);
				case ExpressionType.ArrayLength:
					return Expression.ArrayLength(parameters[0]);
				case ExpressionType.ArrayIndex:
					return Expression.ArrayIndex(parameters[0], parameters[1]);
				case ExpressionType.Call:
					return Expression.Call(((MethodCallExpression)e).Method, parameters);
				case ExpressionType.Coalesce:
					return Expression.Coalesce(parameters[0], parameters[1]);
				case ExpressionType.Conditional:
					return Expression.Condition(parameters[0], parameters[1], parameters[2]);
				case ExpressionType.Constant:
					return e;
				case ExpressionType.Convert:
					return Expression.Convert(parameters[0], ((UnaryExpression)e).Type);
				case ExpressionType.ConvertChecked:
					return Expression.ConvertChecked(parameters[0], ((UnaryExpression)e).Type);
				case ExpressionType.Divide:
					return Expression.Divide(parameters[0], parameters[1]);
				case ExpressionType.Equal:
					return Expression.Equal(parameters[0], parameters[1]);
				case ExpressionType.ExclusiveOr:
					return Expression.ExclusiveOr(parameters[0], parameters[1]);
				case ExpressionType.GreaterThan:
					return Expression.GreaterThan(parameters[0], parameters[1]);
				case ExpressionType.GreaterThanOrEqual:
					return Expression.GreaterThanOrEqual(parameters[0], parameters[1]);
				case ExpressionType.Invoke:
					return Expression.Invoke(((InvocationExpression)e).Expression, parameters);
				case ExpressionType.Lambda:
					return e;
				case ExpressionType.LeftShift:
					return Expression.LeftShift(parameters[0], parameters[1]);
				case ExpressionType.LessThan:
					return Expression.LessThan(parameters[0], parameters[1]);
				case ExpressionType.LessThanOrEqual:
					return Expression.LessThanOrEqual(parameters[0], parameters[1]);
				case ExpressionType.ListInit:
					return e;
				case ExpressionType.MemberAccess:
					return e;
				case ExpressionType.MemberInit:
					return e;
				case ExpressionType.Modulo:
					return Expression.Modulo(parameters[0], parameters[1]);
				case ExpressionType.Multiply:
					return Expression.Multiply(parameters[0], parameters[1]);
				case ExpressionType.MultiplyChecked:
					return Expression.MultiplyChecked(parameters[0], parameters[1]);
				case ExpressionType.Negate:
					return Expression.Negate(parameters[0]);
				case ExpressionType.UnaryPlus:
					return Expression.UnaryPlus(parameters[0]);
				case ExpressionType.NegateChecked:
					return Expression.NegateChecked(parameters[0]);
				case ExpressionType.New:
					return Expression.New(((NewExpression)e).Constructor, parameters);
				case ExpressionType.NewArrayInit:
					return Expression.NewArrayInit(((NewArrayExpression)e).Type, parameters);
				case ExpressionType.NewArrayBounds:
					return Expression.NewArrayBounds(((NewArrayExpression)e).Type, parameters);
				case ExpressionType.Not:
					return Expression.Not(parameters[0]);
				case ExpressionType.NotEqual:
					return Expression.NotEqual(parameters[0], parameters[1]);
				case ExpressionType.Or:
					return Expression.Or(parameters[0], parameters[1]);
				case ExpressionType.OrElse:
					return Expression.OrElse(parameters[0], parameters[1]);
				case ExpressionType.Parameter:
					return e;
				case ExpressionType.Power:
					return Expression.Power(parameters[0], parameters[1]);
				case ExpressionType.Quote:
					return Expression.Quote(parameters[0]);
				case ExpressionType.RightShift:
					return Expression.RightShift(parameters[0], parameters[1]);
				case ExpressionType.Subtract:
					return Expression.Subtract(parameters[0], parameters[1]);
				case ExpressionType.SubtractChecked:
					return Expression.SubtractChecked(parameters[0], parameters[1]);
				case ExpressionType.TypeAs:
					return Expression.TypeAs(parameters[0], ((UnaryExpression)e).Type);
				case ExpressionType.TypeIs:
					return Expression.TypeIs(parameters[0], ((UnaryExpression)e).Type);
				case ExpressionType.Assign:
					return Expression.Assign(parameters[0], parameters[1]);
				case ExpressionType.Block:
					return Expression.Block(parameters);
				case ExpressionType.DebugInfo:
					return e;
				case ExpressionType.Decrement:
					return Expression.Decrement(parameters[0]);
				case ExpressionType.Dynamic:
					return e;
				case ExpressionType.Default:
					return e;
				case ExpressionType.Extension:
					return e;
				case ExpressionType.Goto:
					return e;
				case ExpressionType.Increment:
					return Expression.Increment(parameters[0]);
				case ExpressionType.Index:
					return e;
				case ExpressionType.Label:
					return e;
				case ExpressionType.RuntimeVariables:
					return e;
				case ExpressionType.Loop:
					return Expression.Loop(parameters[0]);
				case ExpressionType.Switch:
					return e;
				case ExpressionType.Throw:
					return Expression.Throw(parameters[0]);
				case ExpressionType.Try:
					return e;
				case ExpressionType.Unbox:
					return Expression.Unbox(parameters[0], ((UnaryExpression)e).Type);
				case ExpressionType.AddAssign:
					return Expression.AddAssign(parameters[0], parameters[1]);
				case ExpressionType.AndAssign:
					return Expression.AndAssign(parameters[0], parameters[1]);
				case ExpressionType.DivideAssign:
					return Expression.DivideAssign(parameters[0], parameters[1]);
				case ExpressionType.ExclusiveOrAssign:
					return Expression.ExclusiveOrAssign(parameters[0], parameters[1]);
				case ExpressionType.LeftShiftAssign:
					return Expression.LeftShiftAssign(parameters[0], parameters[1]);
				case ExpressionType.ModuloAssign:
					return Expression.ModuloAssign(parameters[0], parameters[1]);
				case ExpressionType.MultiplyAssign:
					return Expression.MultiplyAssign(parameters[0], parameters[1]);
				case ExpressionType.OrAssign:
					return Expression.OrAssign(parameters[0], parameters[1]);
				case ExpressionType.PowerAssign:
					return Expression.PowerAssign(parameters[0], parameters[1]);
				case ExpressionType.RightShiftAssign:
					return Expression.RightShiftAssign(parameters[0], parameters[1]);
				case ExpressionType.SubtractAssign:
					return Expression.SubtractAssign(parameters[0], parameters[1]);
				case ExpressionType.AddAssignChecked:
					return Expression.AddAssignChecked(parameters[0], parameters[1]);
				case ExpressionType.MultiplyAssignChecked:
					return Expression.MultiplyAssignChecked(parameters[0], parameters[1]);
				case ExpressionType.SubtractAssignChecked:
					return Expression.SubtractAssignChecked(parameters[0], parameters[1]);
				case ExpressionType.PreIncrementAssign:
					return Expression.PreIncrementAssign(parameters[0]);
				case ExpressionType.PreDecrementAssign:
					return Expression.PreDecrementAssign(parameters[0]);
				case ExpressionType.PostIncrementAssign:
					return Expression.PostIncrementAssign(parameters[0]);
				case ExpressionType.PostDecrementAssign:
					return Expression.PostDecrementAssign(parameters[0]);
				case ExpressionType.TypeEqual:
					return Expression.SubtractAssignChecked(parameters[0], parameters[1]);
				case ExpressionType.OnesComplement:
					return Expression.OnesComplement(parameters[0]);
				case ExpressionType.IsTrue:
					return Expression.IsTrue(parameters[0]);
				case ExpressionType.IsFalse:
					return Expression.IsFalse(parameters[0]);
				default:
					break;
			}
			throw new Exception("L'expression ne peut pas être copiée");
		}


		private bool CheckParameter( Expression e, ParameterInfo parameter )
		{
			if (!parameter.ParameterType.IsAssignableFrom(e.GetType())) return false;
			var attribute = parameter.GetCustomAttributes<ExpressionSignatureAttribute>().FirstOrDefault();
			if (attribute == null) return true;
			return attribute.Match(e);
		}


	}

	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
	public class ExpressionSignatureAttribute : Attribute
	{
		public ExpressionType ExpressionType { get; }

		public ExpressionSignatureAttribute( ExpressionType expressionType )
		{
			this.ExpressionType = expressionType;
		}

		public virtual bool Match( Expression e )
		{
			return e.NodeType == this.ExpressionType;
		}
	}

	public class ExpressionCallSignatureAttribute : ExpressionSignatureAttribute
	{
		public Type Type { get; }
		public string FunctionName { get; }

		public ExpressionCallSignatureAttribute( Type type, string functionName )
			: base(ExpressionType.Call)
		{
			this.Type = type;
			this.FunctionName = functionName;
			
		}
		public override bool Match( Expression e )
		{
			var ec = e as MethodCallExpression;
			return ec != null && ec.Method.DeclaringType == Type && ec.Method.Name == FunctionName;
		}
	}

	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
	public class NumericAttribute : ExpressionSignatureAttribute
	{
		public double[] Values { get; }

		public NumericAttribute() 
			: base(ExpressionType.Constant)
		{
			Values = null;
		}

		public NumericAttribute(params double[] values)
			: base(ExpressionType.Constant)
		{
			Values = values;
		}

		public override bool Match( Expression e )
		{
			var cc = e as ConstantExpression;
			if (!(cc != null && NumberUtils.IsNumeric(cc.Value))) return false;
			if (Values == null) return true;
			return Values.Any(v => v==(double)cc.Value);
		}
	}

}
