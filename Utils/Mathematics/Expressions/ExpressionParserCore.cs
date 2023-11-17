using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Utils.Expressions;

public class ExpressionParserCore
{

    #region fields

    public ParserOptions Options { get; }
    public IBuilder Builder { get; }
    public IResolver Resolver { get; }

    #endregion

    #region ctor

    internal ExpressionParserCore(ParserOptions options, IBuilder builder, IResolver resolver)
    {
        this.Options = options;
        this.Builder = builder;
        this.Resolver = resolver;
    }

    #endregion


    #region methods

    private bool ReadLambdaPrefix(ParserContext context)
    {
        Markers[] markers = [ ('<', '>'), ('(', ')') ];

        int paramIndexPrefix = 0;
        if (context.DefaultInstanceType != null)
        {
            paramIndexPrefix = 1;
        }

        // Check for a lambda prefix (e.g., m=>)
        string val = context.Tokenizer.ReadToken();
        if (val == "(")
        {
            string bracketContent = ParserExtensions.GetBracketString(context, new WrapMarkers("(", ")", null), true);
            if (bracketContent != null)
            {
                string lambdaOperator = context.Tokenizer.ReadToken();
                if (lambdaOperator != "=>")
                {
                    context.Tokenizer.ResetPosition();
                    return false;
                }

                // Parse parameters
                string[] paramsName = Utils.SplitCommaSeparatedList(bracketContent, ',', markers).Select(p => p.Trim()).ToArray();
                for (int i = 0; i < paramsName.Length; i++)
                {
                    string[] typeName = Utils.SplitCommaSeparatedList(paramsName[i], ' ', true, markers).ToArray();
                    Type paramType;
                    string paramName;
                    if (typeName.Length == 1)
                    {
                        paramType = context.ParamTypes?[i + paramIndexPrefix] ?? typeof(object);
                        paramName = paramsName[i];
                        context.Parameters.Add(Expression.Parameter(paramType, paramName));
                    }
                    else if (typeName.Length > 1)
                    {
                        paramType = GetType(context, typeName[0]);
                        if (paramType == null)
                        {
                            throw new ParseUnfindTypeException(typeName[0], context.Tokenizer.Position.Index);
                        }
                        paramName = typeName[1];
                        context.Parameters.Add(Expression.Parameter(paramType, paramName));
                    }
                }
                return true;
            }
        }
        else if (char.IsLetter(val[0]) || val[0] == '_')
        {
            // Parse parameters
            string lambdaOperator = context.Tokenizer.ReadToken();
            if (lambdaOperator == "=>")
            {
                Type paramType = context.ParamTypes?[0 + paramIndexPrefix] ?? typeof(object);
                context.Parameters.Add(Expression.Parameter(paramType, val));
                return true;
            }
        }

        context.Tokenizer.ResetPosition();
        return false;
    }

    public Expression ReadExpression(ParserContext context)
    {
        if (!ReadLambdaPrefix(context) && context.DelegateType != null)
        {
            var parameters = context.DelegateType
                .GetMethod("Invoke")
                ?.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType, p.Name));

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    context.Parameters.Add(parameter);
                }
            }
        }

        return ReadExpression(context, 0, null, out _);
    }

    internal Expression ReadExpression(ParserContext context, int priorityLevel, WrapMarkers markers, out bool isClosedWrap)
    {
        Expression currentExpression = null;
        isClosedWrap = false;
        string val = "";

        // Initialization
        val = context.Tokenizer.ReadToken();

        // Check if the value is null, return null if so
        if (val == null) return null;
        if (markers?.Test(val, out isClosedWrap) ?? false) return null;

        char firstChar = val[0];

        /********************** (Start) First Read, Unary Operation or an Object **************************/

        // Numeric value
        if (char.IsDigit(firstChar))
        {
            currentExpression = Builder.NumberBuilder.Build(this, context, val, priorityLevel, markers, ref isClosedWrap);
        }
        // Non-numeric value
        else
        {
            if (!Builder.StartExpressionBuilders.TryGetValue(val, out var expressionBuilder))
            {
                expressionBuilder = Builder.FallbackUnaryBuilder;
            }
            currentExpression = expressionBuilder.Build(this, context, val, priorityLevel, markers, ref isClosedWrap);
        }
        /********************** (End) First Read, Unary Operation or an Object **************************/

        /********************** (Start) Second (N) Read, All Will Be Binary or Ternary Operations **********************/
        int nextLevel = 0;
        // If isCloseWrap is false (returning immediately when encountering a closing bracket), and the next operator's priority level is higher than the current priority level, then compute the next one.
        while (!isClosedWrap && (nextLevel = TryGetNextPriorityLevel(context)) > priorityLevel)
        {
            string nextVal = context.Tokenizer.ReadToken();

            if (markers?.Test(nextVal, out isClosedWrap) ?? false) return currentExpression;

            if (!Builder.FollowUpExpressionBuilder.TryGetValue(nextVal, out var expressionBuilder))
            {
                expressionBuilder = Builder.FallbackBinaryOrTernaryBuilder;
            }
            currentExpression = expressionBuilder.Build(this, context, currentExpression, val, nextVal, priorityLevel, ref nextLevel, markers, ref isClosedWrap);
        }
        /********************** (End) Second (N) Read, All Will Be Binary or Ternary Operations **********************/

        return currentExpression;
    }

    public Type[] ReadGenericParams(ParserContext context, string startSymbol = "<", string endSymbol = ">")
    {
        if (context.Tokenizer.PeekToken() != startSymbol) return null;

        // Read the opening angle bracket if it hasn't been read previously
        context.Tokenizer.ReadSymbol("<");

        List<Type> result = new();
        while (true)
        {
            // If the next token is '>', then finish reading and break
            if (context.Tokenizer.PeekToken() == endSymbol)
            {
                context.Tokenizer.ReadToken();
                break;
            }

            // Read and add the next type to the list
            var type = ReadType(context, null);
            if (type == null) return null;
            result.Add(type);
        }
        return [.. result];
    }

    public Expression[] ReadExpressions(ParserContext context, WrapMarkers markers, bool readStartSymbol = true, bool ignoreSeparatorAfterBlock = false) 
    {
        if (readStartSymbol)
        {
            if (context.Tokenizer.PeekToken() != markers.Start) return null;
            context.Tokenizer.ReadSymbol(markers.Start);
        }

        List<Expression> result = [];
        // Read parameters
        bool newIsClosedWrap = false;
        while (!newIsClosedWrap)
        {
            Expression expression = ReadExpression(context, 0, markers, out newIsClosedWrap);
            if (!newIsClosedWrap || expression != null) result.Add(expression);
            var nextToken = context.Tokenizer.PeekToken();
            if (!newIsClosedWrap && markers.Test(nextToken, expression is BlockExpression, out var isEnd)) {
                context.Tokenizer.ReadToken();
                if (isEnd) break;
            }
        }
        return [.. result];
    }

    private int TryGetNextPriorityLevel(ParserContext context)
    {
        string nextString = context.Tokenizer.PeekToken();
        if (string.IsNullOrEmpty(nextString) || nextString == ";" || nextString == "}" || nextString == "," || nextString == ":")
        {
            return 0;
        }

        return Options.GetOperatorLevel(nextString, false);
    }

    public Type ReadType(ParserContext context, string val)
    {

        // If the input value is not provided, read it from the code parser
        string strVal = string.IsNullOrEmpty(val) ? context.Tokenizer.ReadToken() : val;

        Type type = null;
        while (type == null)
        {
            List<Type> listGenericType = [];
            // Read generic type parameters
            if (context.Tokenizer.PeekToken() == "<")
            {
                context.Tokenizer.ReadToken();
                while (true)
                {
                    listGenericType.Add(ReadType(context, null));
                    if (context.Tokenizer.PeekToken() == ",")
                    {
                        context.Tokenizer.ReadToken();
                    }
                    else
                    {
                        break;
                    }
                }
                context.Tokenizer.ReadSymbol(">");
            }

            // Try to obtain the Type based on the current string value
            type = Resolver.ResolveType(strVal, [.. listGenericType]);

            if (type == null)
            {
                // If not found, attempt to read a dot (.) and continue with the next part of the type
                bool result = context.Tokenizer.ReadSymbol(".", false);
                if (!result)
                {
                    // If a dot cannot be read, throw an exception indicating that the type is not found
                    throw new ParseUnfindTypeException(strVal, context.Tokenizer.Position.Index);
                }
                strVal += "." + context.Tokenizer.ReadToken();
            }
        }

        return type;
    }

    public Type GetType(ParserContext context, string typeName) => Resolver.ResolveType(typeName);

    public Expression GetExpression(ParserContext context, Expression currentExpression, string name)
    {
        var methods = Resolver.GetInstanceMethods(currentExpression.Type, name);

        if (methods.Any())
        {
            var genericParams = ReadGenericParams(context)?.ToArray();
            var listArguments = ReadExpressions(context, new WrapMarkers("(", ")", ",")).ToArray();

            var methodAndParameters = Resolver.SelectMethod(methods, currentExpression, genericParams, listArguments);

            if (methodAndParameters is not null)
            {
                return Expression.Call(currentExpression, methodAndParameters?.Method, methodAndParameters?.Parameters);
            }
        }

        // Member (PropertyOrField)
        var propertyOrField = Resolver.GetInstancePropertyOrField(currentExpression.Type, name);
        return propertyOrField switch
        {
            PropertyInfo pi => Expression.Property(currentExpression, pi),
            FieldInfo fi => Expression.Field(currentExpression, fi),
            _ => null
        };
    }

    public Expression GetExpression(ParserContext context, Type type, string name)
    {
        var methods = Resolver.GetStaticMethods(type, name);

        if (methods.Any())
        {
            var genericParams = ReadGenericParams(context)?.ToArray();
            var listArguments = ReadExpressions(context, new WrapMarkers("(", ")", ",")).ToArray();

            var methodAndParameters = Resolver.SelectMethod(methods, null, genericParams, listArguments);

            if (methodAndParameters is not null)
            {
                return Expression.Call(null, methodAndParameters?.Method, methodAndParameters?.Parameters);
            }
        }

        // Member (PropertyOrField)
        var propertyOrField = Resolver.GetStaticPropertyOrField(type, name);
        return propertyOrField switch
        {
            PropertyInfo pi => Expression.Property(null, pi),
            FieldInfo fi => Expression.Field(null, fi),
            _ => null
        };
    }



    #endregion
}