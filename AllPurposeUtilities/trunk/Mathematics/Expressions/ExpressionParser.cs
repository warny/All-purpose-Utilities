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
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
	public class ExpressionParser
	{
		private List<GroupDefinition> groupDefinitions;
		private List<OperandDefinition> operandDefinitions;
		private List<ClassDefinition> classDefinitions;

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
							classDefinitions.Add(new ClassDefinition(definition.GetAttribute("assembly"), definition.GetAttribute("name")));
							break;
						default:
							break;
					}
				}
			   
			}

		}

		public LambdaExpression Parse( string stringExpression )
		{
			var m = Regex.Match(stringExpression, @"^\((?<arguments>.*)\)\s*=>(?<expression>.*)$");
			if (!m.Success) {
				throw new System.Data.InvalidExpressionException();
			}
			List<ParameterExpression> parameters = new List<ParameterExpression>();

			foreach (Match args in Regex.Matches(m.Groups["arguments"].Value, @"((?<type>\w+(\.\w+)*)\s+)?(?<name>\w+)")) {
				Type t = args.Groups["type"].Success ? Type.GetType(args.Groups["type"].Value) : typeof(double);
				parameters.Add(Expression.Parameter(t, args.Groups["name"].Value));
			}
			var paramsArray = parameters.ToArray();
			return Expression.Lambda(Parse(m.Groups["expression"].Value, paramsArray), paramsArray);
		}

		public LambdaExpression Parse( string stringExpression, params string[] parameters )
		{
			var expressionParameters = parameters.Select(p=>Expression.Parameter(typeof(double), p)).ToArray();
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
					Regex function = new Regex(@"^(?<Name>\w+)\s*\((?<Arguments>.*)\)$");
					var m = function.Match(stringExpression);
					if (m.Success) {
						var arguments = m.Groups["Arguments"].Value.Split(CultureInfo.TextInfo.ListSeparator[0]);
						Expression[] parametersExpression = arguments.Select(a => Parse(a, parameters)).ToArray();

						foreach (var classDefinition in classDefinitions) {
							var method = classDefinition.GetMethod(m.Groups["Name"].Value, parametersExpression.Select(p=>p.Type).ToArray());
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
			public Type Type { get; }

			public MethodInfo GetMethod( string name, Type[] parametersType )
			{
				try {
					return Type.GetMethod(name, parametersType);
				} catch {
					return null;
				}
			}

			public ClassDefinition( string assembly, string name )
			{
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
