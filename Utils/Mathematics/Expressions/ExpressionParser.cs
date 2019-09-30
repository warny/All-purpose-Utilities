using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Utils.Lists;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
	public class ExpressionParser
	{
		private List<GroupDefinition> groupDefinitions;
		private List<OperandDefinition> operandDefinitions;
		private List<ClassDefinition> classDefinitions;

		private Dictionary<string, ParameterExpression> variables;
		private Dictionary<string, LambdaExpression> expressions;

		public MappedDictionary<string, ParameterExpression> Variables;
		public MappedDictionary<string, LambdaExpression> Expressions;

		public CultureInfo CultureInfo { get; set; }

		public string Culture
		{
			get { return CultureInfo.Name; }
			set {
				if (value == null || value == "invariant") {
					CultureInfo = CultureInfo.InvariantCulture;
				} else {
					CultureInfo = CultureInfo.GetCultureInfo(value);
				}
			}
		}

		public ExpressionParser()
		{
			groupDefinitions = new List<GroupDefinition>();
			operandDefinitions = new List<OperandDefinition>();
			classDefinitions = new List<ClassDefinition>();
			variables = new Dictionary<string, ParameterExpression>();
			Variables = new MappedDictionary<string, ParameterExpression>(GetVariable, RemoveVariable, GetVariables, CountVariables);
			expressions = new Dictionary<string, LambdaExpression>();
			Expressions = new MappedDictionary<string, LambdaExpression>(GetExpression, RemoveExpression, GetExpressions, CountExpressions);

			CultureInfo = CultureInfo. InvariantCulture;
		}

		public static XmlReader SimpleGrammar => new XmlTextReader(new StringReader(Grammar.SimpleGrammar));

		public ExpressionParser(XmlReader definition) : this()
		{
			while (definition.Read()) {
				if (definition.Depth==0) {
					switch (definition.LocalName) {
						case "Grammar":
							Culture = definition.GetAttribute("culture");
							break;
						default:
							break;
					}
				}
				else if (definition.Depth==1) {
					switch (definition.LocalName) {
						case "GroupMarkup":
							groupDefinitions.Add(new GroupDefinition(definition.GetAttribute("open")[0], definition.GetAttribute("close")[0]));
							break;
						case "Operand":
							operandDefinitions.Add(new OperandDefinition(definition.GetAttribute("sign")[0], definition.GetAttribute("constructor")));
							break;
						case "Class":
							classDefinitions.Add(new ClassDefinition(definition.GetAttribute("assembly"), definition.GetAttribute("name"), definition.GetAttribute("alias"), definition.GetAttribute("prefix")));
							break;
						default:
							break;
					}
				}
			   
			}

		}

		public Expression<Func<TResult>> Parse<TResult>( string stringExpression ) 
			=> (Expression<Func<TResult>>)this.Parse(stringExpression, new Type[] { });
		public Expression<Func<TResult, T1>> Parse<TResult, T1>( string stringExpression ) 
			=> (Expression<Func<TResult, T1>>)this.Parse(stringExpression, new Type[] { typeof(T1) });
		public Expression<Func<TResult, T1, T2>> Parse<TResult, T1, T2>( string stringExpression ) 
			=> (Expression<Func<TResult, T1, T2>>)this.Parse(stringExpression, new Type[] { typeof(T1), typeof(T2) });
		public Expression<Func<TResult, T1, T2, T3>> Parse<TResult, T1, T2, T3>( string stringExpression )
			=> (Expression<Func<TResult, T1, T2, T3>>)this.Parse(stringExpression, new Type[] { typeof(T1), typeof(T2), typeof(T3) });
		public Expression<Func<TResult, T1, T2, T3, T4>> Parse<TResult, T1, T2, T3, T4>( string stringExpression )
			=> (Expression<Func<TResult, T1, T2, T3, T4>>)this.Parse(stringExpression, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) });
		public Expression<Func<TResult, T1, T2, T3, T4, T5>> Parse<TResult, T1, T2, T3, T4, T5>( string stringExpression )
			=> (Expression<Func<TResult, T1, T2, T3, T4, T5>>)this.Parse(stringExpression, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) });
		public Expression<Func<TResult, T1, T2, T3, T4, T5, T6>> Parse<TResult, T1, T2, T3, T4, T5, T6>( string stringExpression ) 
			=> (Expression<Func<TResult, T1, T2, T3, T4, T5, T6>>)this.Parse(stringExpression, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6) });
		public Expression<Func<TResult, T1, T2, T3, T4, T5, T6, T7>> Parse<TResult, T1, T2, T3, T4, T5, T6, T7>( string stringExpression )
			=> (Expression<Func<TResult, T1, T2, T3, T4, T5, T6, T7>>)this.Parse(stringExpression, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7) });
		public Expression<Func<TResult, T1, T2, T3, T4, T5, T6, T7, T8>> Parse<TResult, T1, T2, T3, T4, T5, T6, T7, T8>( string stringExpression ) 
			=> (Expression<Func<TResult, T1, T2, T3, T4, T5, T6, T7, T8>>)this.Parse(stringExpression, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8) });
		public Expression<Func<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9>> Parse<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9>( string stringExpression ) 
			=> (Expression<Func<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9>>)this.Parse(stringExpression, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9) });
		public Expression<Func<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>> Parse<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>( string stringExpression ) 
			=> (Expression<Func<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>)this.Parse(stringExpression, new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8), typeof(T9), typeof(T10)});

		public LambdaExpression Parse( string stringExpression )
		{
			return Parse(stringExpression, new Type[] { });
		}

		public LambdaExpression Parse( string stringExpression, params Type[] argumentsTypes )
		{
			var m = Regex.Match(stringExpression, @"^(?<name>\w+)?\((?<arguments>.*)\)\s*=>(?<expression>.*)$");
			if (!m.Success) {
				throw new System.Data.InvalidExpressionException();
			}
			List<ParameterExpression> parameters = new List<ParameterExpression>();
			int argumentPosition = 0;
			var matches = Regex.Matches(m.Groups["arguments"].Value, @"((?<type>\w+(\.\w+)*)\s+)?(?<name>\w+)");
			foreach (Match args in matches) {
				Type t;
				if (args.Groups["type"].Success) {
					string typeName = args.Groups["type"].Value;
					var classDefinition = classDefinitions.FirstOrDefault(cd => cd.Alias == typeName);
					if (classDefinition != null) {
						t = classDefinition.Type;
					} else {
						t = Type.GetType(args.Groups["type"].Value);
					}
				} else if (argumentsTypes!= null && argumentsTypes.Length > argumentPosition) {
					t = argumentsTypes[argumentPosition];
				} else {
					t = typeof(double);
				}
				parameters.Add(Expression.Parameter(t, args.Groups["name"].Value));
				argumentPosition++;
			}
			var paramsArray = parameters.ToArray();
			LambdaExpression expression;

			if (m.Groups["name"].Success) {
				expression = Expression.Lambda(Parse(m.Groups["expression"].Value, paramsArray), m.Groups["name"].Value, paramsArray);
				AddLambdaExpression(expression);
			} else {
				expression = Expression.Lambda(Parse(m.Groups["expression"].Value, paramsArray), paramsArray);
			}

			return expression;
		}

		public LambdaExpression Parse( string stringExpression, string[] parameters, Type[] parametersTypes )
		{
			if (parameters.Length != parametersTypes.Length) {
				throw new ArgumentException("Le nombre d'arguments doit être égal au nombre de types");
			}

			var expressionParameters = parameters.Select((p,i)=>Expression.Parameter(parametersTypes[i], p)).ToArray();
			return Expression.Lambda(Parse(stringExpression, expressionParameters), expressionParameters);
		}

		public Expression Parse( string stringExpression, params ParameterExpression[] parameters )
		{
			stringExpression = stringExpression.Trim();
			Stack<GroupDefinition> brackets = new Stack<GroupDefinition>();
			OperandDefinition currentOperand = null;
			int operandPosition = -1, operandPriority = int.MaxValue;


			for (int i = stringExpression.Length - 1 ; i >= 0 ; i--) {
				char c = stringExpression[i];

				var groupDefinition = groupDefinitions.FirstOrDefault(gd => gd.Open == c || gd.Close == c);
				if (groupDefinition!=null) {
					if (groupDefinition.Close == c) brackets.Push(groupDefinition);
					else if (groupDefinition.Open == c) {
						if (groupDefinition != brackets.Pop()) {
							throw new System.Data.InvalidExpressionException();
						}
					}
				}
				if (brackets.Count > 0) continue;

				var operands = operandDefinitions.Select(( operand, priority ) => new { operand, priority }).Where(od => od.operand.Sign==c);
				if (!operands.Any()) {
					continue;
				} else if (i==0) {
					var operand = operands.FirstOrDefault(o => o.operand.OperandType == typeof(UnaryExpression));
					if (operand.priority < operandPriority) {
						operandPosition = i;
						currentOperand = operand.operand;
						operandPriority = operand.priority;
					}
				} else {
					var operand = operands.FirstOrDefault(o => o.operand.OperandType == typeof(BinaryExpression));
					if (operand.priority < operandPriority) {
						operandPosition = i;
						currentOperand = operand.operand;
						operandPriority = operand.priority;
					}
				}
			}

			if (brackets.Count > 0) {
				throw new System.Data.InvalidExpressionException();
			}

			if (operandPosition == -1) {
				double value;
				if (double.TryParse(stringExpression, NumberStyles.Float, CultureInfo, out value)) {
					return Expression.Constant(value);
				} else if (stringExpression.Contains("(")) {
					Regex function = new Regex(@"^(?<prefix>(\w+\.)+)?(?<Name>\w+)\s*\((?<Arguments>.*)\)$");
					var m = function.Match(stringExpression);
					var arguments = m.Groups["Arguments"].Value.Split(CultureInfo.TextInfo.ListSeparator[0]);
					Expression[] parametersExpression = arguments.Select(a => Parse(a, parameters)).ToArray();
					Type[] parametersTypes = parametersExpression.Select(p => p.Type).ToArray();

					if (m.Success) {
						if (m.Groups["prefix"].Success) {
							string prefix = m.Groups["prefix"].Value;
							var variable = Variables[prefix];
							if (variable != null) {
								return Expression.Call(variable, m.Groups["Name"].Value, parametersTypes, parametersExpression);
							}

							var classDefinition = classDefinitions.FirstOrDefault(cd=>cd.Prefix == prefix);
							if (classDefinition != null) {
								var method = classDefinition.GetMethod(m.Groups["Name"].Value, parametersTypes);
								if (method != null) {
									return Expression.Call(method, parametersExpression);
								}
							}
							throw new System.Data.InvalidExpressionException();
						}

						var expression = Expressions[m.Groups["Name"].Value];
						if (expression != null) {
							return Expression.Invoke(expression, parametersExpression);
						}

						foreach (var classDefinition in classDefinitions.Where(cd=>cd.Prefix == null && cd.Alias == null)) {
							var method = classDefinition.GetMethod(m.Groups["Name"].Value, parametersTypes);
							if (method != null) {
								return Expression.Call(method, parametersExpression);
							}
						}
					}
					throw new System.Data.InvalidExpressionException();
				} else {
					var parameter = parameters.FirstOrDefault(p=>p.Name == stringExpression);
					if (parameter!= null) {
						return parameter;
					} else {
						value = (double)(typeof(Math).GetField(stringExpression)?.GetValue(null) ?? typeof(Math).GetProperty(stringExpression)?.GetValue(null));
						return Expression.Constant(typeof(Math).GetField(stringExpression).GetValue(null));
					}
				}
			} else if (currentOperand.OperandType== typeof(BinaryExpression)) {
				string left = StringUtils.TrimBrackets(stringExpression.Substring(0, operandPosition), groupDefinitions.ToArray());
				string right = StringUtils.TrimBrackets(stringExpression.Substring(operandPosition + 1), groupDefinitions.ToArray());

				return (Expression)currentOperand.Constructor.Invoke(null, new[] { Parse(left, parameters), Parse(right, parameters) });
			} else if (currentOperand.OperandType== typeof(UnaryExpression)) {
				return (Expression)currentOperand.Constructor.Invoke(null, new[] { Parse(stringExpression.Substring(1), parameters) });
			}
			throw new System.Data.InvalidExpressionException();
		}

		public void AddVariable( ParameterExpression variable )
		{
			if (variable.Name == null) throw new ArgumentNullException(nameof(variable), $"Le champs {nameof(variable.Name)} doit être défini");
			variables.Add(variable.Name, variable);
		}

		private ParameterExpression GetVariable( string name )
		{
			ParameterExpression result;
			if (variables.TryGetValue(name, out result)) {
				return result;
			}
			return null;
		}

		private bool RemoveVariable( string name )
		{
			return variables.Remove(name);
		}

		private IEnumerable<KeyValuePair<string, ParameterExpression>> GetVariables()
		{
			return variables;
		}

		private int CountVariables()
		{
			return variables.Count;
		}

		public void AddLambdaExpression( LambdaExpression expression )
		{
			if (expression.Name == null) throw new ArgumentNullException(nameof(expression), $"Le champs {nameof(expression.Name)} doit être défini");
			expressions.Add(expression.Name, expression);
		}

		private LambdaExpression GetExpression( string name )
		{
			LambdaExpression result;
			if (expressions.TryGetValue(name, out result)) {
				return result;
			}
			return null;
		}

		private bool RemoveExpression( string name )
		{
			return expressions.Remove(name);
		}

		private IEnumerable<KeyValuePair<string, LambdaExpression>> GetExpressions()
		{
			return expressions;
		}

		private int CountExpressions()
		{
			return expressions.Count;
		}

		class GroupDefinition : Brackets, IEquatable<GroupDefinition>
		{
			public GroupDefinition( char open, char close ) : base(open, close) { }

			public override bool Equals( object obj )
			{
				if (!(obj is GroupDefinition)) return false;
				return this.Equals((GroupDefinition)obj);
			}

			public override int GetHashCode()
			{
				return 23*Open.GetHashCode() << 1 + Close.GetHashCode();
			}

			public bool Equals( GroupDefinition other )
			{
				return this.Open == other.Open && this.Close == other.Close;
			}
		}

		private class OperandDefinition
		{
			public char Sign { get; }
			public MethodInfo Constructor { get; }
			public Type OperandType { get; }

			public OperandDefinition( char sign, string constructor )
			{
				this.Sign = sign;
				this.Constructor = typeof(Expression).GetMethods().First(m=>m.Name==constructor);

				var parameters = this.Constructor.GetParameters ();

				if (parameters[0].Name=="expression") this.OperandType = typeof(UnaryExpression);
				else if (parameters[0].Name=="left" && parameters[1].Name=="right") this.OperandType = typeof(BinaryExpression);

			}
		}

		private class ClassDefinition
		{
			public string Alias { get; }
			public string Prefix { get; }
			public Type Type { get; }

			public MethodInfo GetMethod( string name, Type[] parametersType )
			{
				try {
					return Type.GetMethod(name, parametersType);
				} catch {
					return null;
				}
			}

			public ClassDefinition( string assembly, string name, string alias, string prefix )
			{
				this.Alias = alias;
				this.Prefix = prefix;

				Assembly a;
				if (string.IsNullOrWhiteSpace(assembly)) {
					a = Assembly.Load("mscorlib");
				} else {
					a = Assembly.Load(assembly);
				}
				Type = a.GetType(name);
			}
		}

	}
}
