using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Utils.Expressions.Builders;
using Utils.Expressions.Resolvers;

namespace Utils.Expressions;

/// <summary>
/// Lambda expression parser
/// </summary>
static public class ExpressionParser
{
    #region all Parse()

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "namespaces"> namespace set </param>
    public static LambdaExpression Parse(string lambdaCode, string[] namespaces = null, IReadOnlyDictionary<string, object> constants = null)
    {
        return ParseCore([], lambdaCode, null, false, namespaces);
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name="defaultInstance"></param>
    /// <param name = "namespaces"> namespace set </param>
    static public LambdaExpression Parse(string lambdaCode, Type defaultInstance, string[] namespaces = null, IReadOnlyDictionary<string, object> constants = null)
    {
        return ParseCore<Delegate>(null, lambdaCode, defaultInstance, false, namespaces);
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name="paramTypes"></param>
    /// <param name = "namespaces"> namespace set </param>
    /// <param name="defaultInstance"></param>
    static public LambdaExpression Parse(string[] paramNames, string lambdaCode, Type defaultInstance, string[] namespaces = null)
    {
        return ParseCore<Delegate>(paramNames, lambdaCode, defaultInstance, false, namespaces);
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "delegateType"> delegate type </param>
    /// <param name = "namespaces"> namespace set </param>
    static public LambdaExpression Parse(Type delegateType, string lambdaCode, string[] namespaces = null)
    {
        return ParseCore<Delegate>(null, lambdaCode, null, false, namespaces);
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "delegateType"> delegate type </param>
    /// <param name = "firstTypeIsDefaultInstance"> whether the first type is the default instance </param>
    /// <param name = "namespaces"> namespace set </param>
    static public LambdaExpression Parse(Type delegateType, string lambdaCode, bool firstTypeIsDefaultInstance, string[] namespaces = null)
    {
        return ParseCore<Delegate>(null, lambdaCode, null, firstTypeIsDefaultInstance, namespaces);
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "namespaces"> namespace set </param>
    static public Expression<TDelegate> Parse<TDelegate>(string lambdaCode, string[] namespaces = null)
    {
        return (Expression<TDelegate>)ParseCore<TDelegate>(null, lambdaCode, null, false, namespaces);
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "firstTypeIsDefaultInstance"> whether the first type is the default instance </param>
    /// <param name = "namespaces"> namespace set </param>
    static public Expression<TDelegate> Parse<TDelegate>(string lambdaCode, bool firstTypeIsDefaultInstance, string[] namespaces = null)
    {
        return (Expression<TDelegate>)ParseCore<TDelegate>(null, lambdaCode, null, firstTypeIsDefaultInstance, namespaces);
    }

    private static LambdaExpression Parse(ParameterExpression[] parameters, string lambdaCode, Type defaultStaticType, bool firstTypeIsDefaultInstance, string[] namespaces)
    {
        return ParseCore(parameters, lambdaCode, defaultStaticType, firstTypeIsDefaultInstance, namespaces);
    }



    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "firstTypeIsDefaultInstance"> whether the first type is the default instance </param>
    /// <param name = "namespaces"> namespace set </param>
    static public Expression<TDelegate> Parse<TDelegate>(string lambdaCode, ParameterExpression[] parameters, string[] namespaces = null)
    {
        return (Expression<TDelegate>)ParseCore(parameters, lambdaCode, null, false, namespaces);
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "firstTypeIsDefaultInstance"> whether the first type is the default instance </param>
    /// <param name = "namespaces"> namespace set </param>
    static public Expression<TDelegate> Parse<TDelegate>(string lambdaCode, ParameterExpression[] parameters, Type defaultStaticType, bool firstTypeIsDefaultInstance, string[] namespaces = null)
    {
        return (Expression<TDelegate>)ParseCore(parameters, lambdaCode, defaultStaticType, firstTypeIsDefaultInstance, namespaces);
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "firstTypeIsDefaultInstance"> whether the first type is the default instance </param>
    /// <param name = "namespaces"> namespace set </param>
    static public LambdaExpression Parse(string lambdaCode, ParameterExpression[] parameters, Type defaultStaticType, bool firstTypeIsDefaultInstance, string[] namespaces = null)
    {
        return ParseCore(parameters, lambdaCode, defaultStaticType, firstTypeIsDefaultInstance, namespaces);
    }
    #endregion

    #region all Compile()

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "namespaces"> namespace set </param>
    static public Delegate Compile(string lambdaCode, params string[] namespaces)
    {
        return Parse(lambdaCode, namespaces).Compile();
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name="defaultInstance">type Of the default instance</param>
    /// <param name = "namespaces"> namespace set </param>
    static public Delegate Compile(string lambdaCode, Type defaultInstance, params string[] namespaces)
    {
        return Parse(namespaces, lambdaCode, defaultInstance).Compile();
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name="delegateType">delegate type</param>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "namespaces"> namespace set </param>
    static public Delegate Compile(Type delegateType, string lambdaCode, params string[] namespaces)
    {
        return Parse(delegateType, lambdaCode, namespaces).Compile();
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name="delegateType">delegate type</param>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "firstTypeIsDefaultInstance"> whether the first type is the default instance </param>
    /// <param name = "namespaces"> namespace set </param>
    static public Delegate Compile(Type delegateType, string lambdaCode, bool firstTypeIsDefaultInstance, params string[] namespaces)
    {
        return Parse(delegateType, lambdaCode, firstTypeIsDefaultInstance, namespaces).Compile();
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "namespaces"> namespace set </param>
    static public TDelegate Compile<TDelegate>(string lambdaCode, params string[] namespaces)
    {
        return Parse<TDelegate>(lambdaCode, namespaces).Compile();
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "firstTypeIsDefaultInstance"> whether the first type is the default instance </param>
    /// <param name = "namespaces"> namespace set </param>
    static public TDelegate Compile<TDelegate>(string lambdaCode, bool firstTypeIsDefaultInstance, params string[] namespaces)
    {
        return Parse<TDelegate>(lambdaCode, firstTypeIsDefaultInstance, namespaces).Compile();
    }

    #endregion

    #region all Exec()

    /// <summary>
    /// Execute the code with instance as the context $ ($ 0 for instance, (can be omitted $ 0); $ 1 that objects of the first object; $ 2 that objects of the second object ....)
    /// </summary>
    /// <typeparam name = "T"> returned result type </typeparam>
    /// <param name = "instance"> execute the code with this object as the context ($ 0 in code, $ 0 can be omitted) </param>
    /// <param name = "code"> executed code </param>
    /// <param name="defaultStaticType">type used to resolve static methods</param>
    /// <param name = "namespaces"> import namespace </param>
    /// <param name = "objects"> parameter object </param>
    /// <returns> </returns>
    static public T Exec<T>(object instance, string code, Type defaultStaticType, string[] namespaces, params object[] objects)
    {
        object[] allObjects = [instance, .. objects];
        var parameters = allObjects.Select((obj, i) => Expression.Parameter(obj?.GetType() ?? typeof(object), "Param" + i)).ToArray();

        object[] inputObjs = new object[objects.Length + 2];
        inputObjs[1] = inputObjs[0] = instance;
        Array.Copy(objects, 0, inputObjs, 2, objects.Length);

        string lambdaParams = string.Join(",", allObjects.Select((m, i) => "$" + i).ToArray());
        Type[] paramTypes = inputObjs.Select(m => m.GetType()).ToArray();

        string newCode = string.Format("({0})=>{1}", lambdaParams, code);

        return (T)Parse(parameters, newCode, defaultStaticType, instance != null, namespaces).Compile().DynamicInvoke(inputObjs);
    }

    /// <summary>
    /// Execute the code with instance as the context $ ($ 0 for instance, (can be omitted $ 0); $ 1 that objects of the first object; $ 2 that objects of the second object ....)
    /// </summary>
    /// <param name = "instance"> execute the code with this object as the context ($ 0 in code, $ 0 can be omitted) </ param>
    /// <param name = "code"> executed code </param>
    /// <param name = "namespaces"> import namespace </param>
    /// <param name = "objects"> parameter object </param>
    /// <returns> </returns>
    static public object Exec(object instance, string code, string[] namespaces, params object[] objects)
    {
        return Exec<object>(instance, code, null, namespaces, objects);
    }

    #endregion

    #region Parse

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name="delegateType">Type of delegate</param>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "namespaces"> namespace set </param>
    /// <param name="defaultStaticType"></param>
    /// <param name="firstTypeIsDefaultInstance"></param>
    /// <exception cref="ArgumentNullException">Exception thrown if <paramref name="code"/> is null</exception>
    static private LambdaExpression ParseCore<TDelegate>(string[] paramNames, string lambdaCode, Type defaultStaticType, bool firstTypeIsDefaultInstance, string[] namespaces = null, IReadOnlyDictionary<string, object> constants = null)
    {
        var options = new ParserOptions();
        var builder = new CStyleBuilder();
        var tokenizer = new Tokenizer(lambdaCode ?? throw new ArgumentNullException(nameof(lambdaCode)), builder);
        var parser = new ExpressionParserCore(options, builder, new DefaultResolver(new TypeFinder(options, namespaces, []), constants));
        var context = new ParserContext(typeof(TDelegate), paramNames, defaultStaticType, tokenizer, firstTypeIsDefaultInstance);
        return Expression.Lambda(parser.ReadExpression(context), context.Parameters);
    }

    /// <summary>
    /// parse the Lambda expression code
    /// </summary>
    /// <param name = "lambdaCode"> lambda expression code. Such as: m => m.ToString () </param>
    /// <param name = "namespaces"> namespace set </param>
    /// <param name="defaultStaticType"></param>
    /// <param name="firstTypeIsDefaultInstance"></param>
    /// <exception cref="ArgumentNullException">Exception thrown if <paramref name="code"/> is null</exception>
    static private LambdaExpression ParseCore(ParameterExpression[] parameters, string lambdaCode, Type defaultStaticType, bool firstTypeIsDefaultInstance, string[] namespaces = null, IReadOnlyDictionary<string, object> constants = null)
    {
        var options = new ParserOptions();
        var builder = new CStyleBuilder();
        var tokenizer = new Tokenizer(lambdaCode ?? throw new ArgumentNullException(nameof(lambdaCode)), builder);
        var parser = new ExpressionParserCore(options, builder, new DefaultResolver(new TypeFinder(options, namespaces, []), constants));
        var context = new ParserContext(parameters, defaultStaticType, tokenizer, firstTypeIsDefaultInstance);
        return Expression.Lambda(parser.ReadExpression(context), context.Parameters);
    }


    /// <summary>
    /// Parse the expression code into a simple expression
    /// </summary>
    /// <param name="code">Code to be parsed</param>
    /// <param name="parameters">Variables to be used</param>
    /// <param name="defaultStaticType"></param>
    /// <param name="namespaces">Default resolution namespaces</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">Exception thrown if <paramref name="code"/> is null</exception>
    static public Expression ParseExpression(string code, ParameterExpression[] parameters, Type defaultStaticType, bool defaultFirst, string[] namespaces = null, IReadOnlyDictionary<string, object> constants = null)
    {
        var options = new ParserOptions();
        var builder = new CStyleBuilder();
        var tokenizer = new Tokenizer(code ?? throw new ArgumentNullException(nameof(code)), builder);
        var parser = new ExpressionParserCore(options, builder, new DefaultResolver(new TypeFinder(options, namespaces, []), constants));
        ParserContext context = new ParserContext(parameters, defaultStaticType, tokenizer, defaultFirst);
        foreach (var parameter in parameters)
        {
            context.Parameters.Add(parameter);
        }
        return parser.ReadExpression(context);
    }

    #endregion
}