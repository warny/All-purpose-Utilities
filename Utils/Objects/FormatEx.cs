using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Collections;

namespace Utils.Objects
{
	public class FormatEx : IPreparedFormat
	{
		/*
		public Dictionary<char, GetConstantDelegate> GetConstants { get; set; }
		public Overrides Overrides { get; set; }

		LRUCache<(string expression, Type type), Expression> compiledExpressions = new LRUCache<(string expression, Type type), Expression>(500);

		public static Regex expressionParser = new Regex(@"^((?<command>[a-z]\w*)|\[(?<indexcommand>(?>\[(?<DEPTH>)|\](?<-DEPTH>)|.+?)*)\](?(DEPTH)(?!))|\$(?<indexstring>[a-z]\w*)|\$(?<index>\d+)|\""(?<string>([^""]|\""\"")*)\""|(?<number>\d+(\.\d+)?)(?<modifier>[bsilfd])?)((\((?<parenthesis>(?>\((?<DEPTH>)|\)(?<-DEPTH>)|[^()]+)*)\)(?(DEPTH)(?!)))|(\[(?<brackets>(?>\[(?<DEPTH>)|\](?<-DEPTH>)|.+?)*)\](?(DEPTH)(?!))))?(\.(?<subexpression>.+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
		public static Regex subExpressionParser = new Regex(@"^((?<command>[a-z]\w*)|\[(?<indexcommand>(?>\[(?<DEPTH>)|\](?<-DEPTH>)|.+?)*)\](?(DEPTH)(?!)))((\((?<parenthesis>(?>\((?<DEPTH>)|\)(?<-DEPTH>)|[^()]+)*)\)(?(DEPTH)(?!)))|(\[(?<brackets>(?>\[(?<DEPTH>)|\](?<-DEPTH>)|.+?)*)\](?(DEPTH)(?!))))?(\.(?<subexpression>.+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		protected Expression Compile( object startObject, string expression )
		{
			Match parsedExpression = expressionParser.Match(expression);
			Type objectType = startObject.GetType();
			Type objectGenericType = objectType.GetGenericTypeDefinition();

			if (objectGenericType.IsAssignableFrom(typeof(Dictionary<,>)))
			{
				if (parsedExpression.Groups["indexstring"].Success)
				{
					object key;
					if (objectType.GenericTypeArguments[0] == typeof(string))
					{
						key = parsedExpression.Groups["indexstring"].Value;
					}
					else
					{
						key = Parsers.Parse(parsedExpression.Groups["indexstring"].Value, objectType.GenericTypeArguments[0]);
					}
					//var baseObject = 
					
				} else if(parsedExpression.Groups["index"].Success) {
				}
			}
			
		}
		*/
		public Dictionary<char, GetConstantDelegate> GetConstants { get; set; }
		public Overrides Overrides { get; set; }
	}
}
