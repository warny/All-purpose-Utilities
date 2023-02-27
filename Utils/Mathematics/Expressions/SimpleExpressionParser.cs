using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Utils.Collections;

namespace Utils.Mathematics.Expressions
{
	public class SimpleExpressionParser
	{
		private readonly IndexedList<string, BinaryOperator> Operators = new IndexedList<string, BinaryOperator>(o => o.Name);

		public SimpleExpressionParser() : this(BinaryOperator.DefaultOperators) { }

		public SimpleExpressionParser(IEnumerable<BinaryOperator> operators)
		{
			foreach (var op in operators)
			{
				Operators.Add(op);
			}
		}


		private string[] Tokenize(string input)
		{
			// Diviser l'entrée en tokens en utilisant les délimiteurs suivants : +, -, *, /, %, (, ), et espaces.
			var operators = "(" + string.Join("|", Operators.Keys.Select(o => "\\" + o)) + "|\\(|\\)|\\,|\\s+)";

			return Regex.Split(input, operators)
				.Where(s => !string.IsNullOrEmpty(s))
				.ToArray();
		}

		public Expression ParseExpression(string expression, params ParameterExpression[] parameters)
			=> ParseExpression(expression, new IndexedList<string, ParameterExpression>(e=>e.Name, parameters));

		public Expression ParseExpression(string expression, IEnumerable<ParameterExpression> parameters)
			=> ParseExpression(expression, new IndexedList<string, ParameterExpression>(e => e.Name, parameters));

		private Expression ParseExpression(string expression, IndexedList<string, ParameterExpression> parameters)
		{
			var tokens = Tokenize(expression);
			var outputQueue = new Queue<string>();
			var operatorStack = new Stack<string>();
			foreach (var token in tokens)
			{
				if (double.TryParse(token, out double value))
				{
					// Token est un nombre, ajouter directement à la file de sortie
					outputQueue.Enqueue(token);
				}
				else if ( parameters.ContainsKey(token))
				{
					// Token est la variable, ajouter directement à la file de sortie
					outputQueue.Enqueue(token);
				}
				else if (typeof(Math).GetMethod(token) is MethodInfo func && func.GetParameters().Length == 1)
				{
					// Token est une fonction, empiler sur la pile d'opérateurs
					operatorStack.Push(token);
				}
				else if (token == ",")
				{
					// Token est une virgule, dépiler les opérateurs de la pile et ajouter à la file de sortie jusqu'à ce que l'on atteigne une parenthèse ouvrante
					while (operatorStack.Count > 0 && operatorStack.Peek() != "(")
					{
						outputQueue.Enqueue(operatorStack.Pop());
					}
				}
				else if (GetPrecedence(token) > 0)
				{
					// Token est un opérateur, dépiler les opérateurs de la pile et ajouter à la file de sortie
					while (operatorStack.Count > 0 && GetPrecedence(operatorStack.Peek()) >= GetPrecedence(token))
					{
						outputQueue.Enqueue(operatorStack.Pop());
					}
					// Empiler le nouvel opérateur sur la pile d'opérateurs
					operatorStack.Push(token);
				}
				else if (token == "(")
				{
					// Token est une parenthèse ouvrante, empiler sur la pile d'opérateurs
					operatorStack.Push(token);
				}
				else if (token == ")")
				{
					// Token est une parenthèse fermante, dépiler les opérateurs de la pile et ajouter à la file de sortie jusqu'à ce que l'on atteigne une parenthèse ouvrante
					while (operatorStack.Count > 0 && operatorStack.Peek() != "(")
					{
						outputQueue.Enqueue(operatorStack.Pop());
					}
					if (operatorStack.Count == 0)
					{
						throw new ArgumentException("Mismatched parentheses in expression");
					}
					// Vérifier si la fonction précédant la parenthèse ouvrante est empilée sur la pile d'opérateurs et si oui, l'ajouter à la file de sortie
					if (typeof(Math).GetMethod(operatorStack.Peek()) is MethodInfo f && f.GetParameters().Length == 1)
					{
						outputQueue.Enqueue(operatorStack.Pop());
					}
					// Dépiler la parenthèse ouvrante
					operatorStack.Pop();
				}
				else
				{
					throw new ArgumentException($"Unrecognized token: {token}");
				}
			}

			// Dépiler les opérateurs restants de la pile et ajouter à la file de sortie
			while (operatorStack.Count > 0)
			{
				var op = operatorStack.Pop();
				if (op == "(")
				{
					throw new ArgumentException("Mismatched parentheses in expression");
				}
				outputQueue.Enqueue(op);
			}

			// Vérifier que toutes les parenthèses ont été correctement fermées
			if (operatorStack.Count > 0)
			{
				throw new ArgumentException("Mismatched parentheses in expression");
			}

			// Convertir la file de sortie en tableau de tokens
			var postfixTokens = outputQueue.ToArray();

			// Analyser les tokens en notation postfixée pour obtenir une expression d'arbre d'expression
			var stack = new Stack<Expression>();
			foreach (var token in postfixTokens)
			{
				if (double.TryParse(token, out double value))
				{
					// Token est un nombre, empiler une constante sur la pile
					stack.Push(Expression.Constant(value));
				}
				else if (parameters.TryGetValue(token, out var parameter))
				{
					// Token est la variable, empiler la variable sur la pile
					stack.Push(parameter);
				}
				else if (typeof(Math).GetMethod(token) is MethodInfo func && func.GetParameters().Length == 1)
				{
					// Token est une fonction, dépiler l'argument de la pile et appliquer la fonction
					var arg = stack.Pop();
					stack.Push(Expression.Call(null, func, arg));
				}
				else if (GetPrecedence(token) > 0)
				{
					// Token est un opérateur, dépiler les arguments de la pile et appliquer l'opérateur
					var right = stack.Pop();
					var left = stack.Pop();
					stack.Push(ApplyOperator(token, left, right));
				}
				else
				{
					throw new ArgumentException($"Unrecognized token: {token}");
				}
			}

			// Vérifier que la pile contient exactement une expression
			if (stack.Count != 1)
			{
				throw new ArgumentException("Invalid expression");
			}

			return Expression.Lambda(stack.Pop(), parameters);
		}

		// Méthode utilitaire pour appliquer un opérateur à deux expressions
		private Expression ApplyOperator(string op, Expression left, Expression right)
		{
			return Operators[op].GetExpression(left, right);
		}

		// Méthode utilitaire pour obtenir la précédence d'un opérateur
		private int GetPrecedence(string op)
		{
			if (Operators.TryGetValue(op, out var result))
			{
				return result.Precedence;
			}
			return 0;
		}
	}

	public class BinaryOperator {
		public BinaryOperator(string name, int precedence, Func<Expression, Expression, Expression> getExpression)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.Precedence = precedence;
			this.GetExpression = getExpression ?? throw new ArgumentNullException(nameof(getExpression));
		}

		public string Name { get; }
		public int Precedence { get; }
		public Func<Expression, Expression, Expression> GetExpression { get; }


		public static readonly BinaryOperator[] DefaultOperators = {
			new BinaryOperator("+", 1, Expression.Add),
			new BinaryOperator("-", 1, Expression.Subtract),
			new BinaryOperator("*", 2, Expression.Multiply),
			new BinaryOperator("/", 2, Expression.Divide),
			new BinaryOperator("%", 2, Expression.Modulo),
			new BinaryOperator("^", 3, Expression.Power)
		};
	}
}
