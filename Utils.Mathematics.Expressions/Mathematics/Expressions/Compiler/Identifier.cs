using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Mathematics.Expressions.Compiler
{
	public class Identifier : ExpressionTreeWithLeft
	{
		public string Name { get; set; }
		internal IdentifierTypeEnum IdentifierType { get; private set; }
		internal string IdentifierFullName { get; set; }
		internal Type Type { get; private set; }

		public override Expression[] CreateExpression(Context context)
		{
			if (Left == null)
			{
				if (context.Variables.TryGetValue(Name, out var variable))
				{
					IdentifierType = IdentifierTypeEnum.Object;
					return new[] { variable };
				}
				var type = Type.GetType(Name);
				if (type != null)
				{
					IdentifierType = IdentifierTypeEnum.Class;
					Type = type;
					return null;
				}
				Type = null;
				IdentifierFullName = Name;
				IdentifierType = IdentifierTypeEnum.Namespace;
				return null;
			}

			Expression LeftExpression = Left.CreateExpression(context)?.ToExpression();
			if (LeftExpression == null)
			{
				if (Left is Identifier leftIdentifier)
				{
					IdentifierFullName = leftIdentifier.IdentifierFullName + "." + Name;
					switch (leftIdentifier.IdentifierType)
					{
						case IdentifierTypeEnum.Namespace:
							Type type = Type.GetType(IdentifierFullName);
							if (type == null)
							{
								Type = null;
								IdentifierType = IdentifierTypeEnum.Namespace;
								return null;
							}
							else
							{
								Type = type;
								IdentifierType = IdentifierTypeEnum.Class;
								return null;
							}
						case IdentifierTypeEnum.Class:
							var field = leftIdentifier.Type.GetField(Name, BindingFlags.Static | BindingFlags.Public);
							if (field != null)
							{
								IdentifierType = IdentifierTypeEnum.Object;
								return new[] { Expression.Field(null, field) };
							}
							var property = leftIdentifier.Type.GetProperty(Name, BindingFlags.Static | BindingFlags.Public);
							if (property != null)
							{
								IdentifierType = IdentifierTypeEnum.Object;
								return new[] { Expression.Property(null, property) };
							}
							var nestedType = leftIdentifier.Type.GetNestedType(Name);
							if (nestedType != null)
							{
								IdentifierType = IdentifierTypeEnum.Class;
								Type = nestedType;
								return null;
							}
							throw new CompilerException("Impossible de résoudre le nom", leftIdentifier.IdentifierFullName + "." + Name);
						default:
							throw new Exception("Type invalide");
					}
				}
				throw new CompilerException("Impossible de résoudre le nom", Name);
			}
			else
			{
				Type type = LeftExpression.Type;
				var field = type.GetField(Name, BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public);
				if (field != null)
				{
					IdentifierType = IdentifierTypeEnum.Object;
					return new[] { Expression.Field(LeftExpression, field) };
				}
				var property = type.GetProperty(Name, BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public);
				if (property != null)
				{
					IdentifierType = IdentifierTypeEnum.Object;
					return new[] { Expression.Property(LeftExpression, property) };
				}
				throw new CompilerException("Impossible de résoudre le nom", Name);
			}
		}
	}

	public enum IdentifierTypeEnum
	{
		Namespace,
		Class,
		Object
	}
}