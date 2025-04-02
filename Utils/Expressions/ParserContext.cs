using System.Linq.Expressions;
using System.Reflection;
using Utils.Objects;

namespace Utils.Expressions;

public class ParserContext
{
    public ParserContext(Type delegateType, string[] paramNames, Type defaultStaticType, Tokenizer tokenizer, bool firstArgumentIsDefaultInstance)
    {
        DelegateType = delegateType.Arg().MustNotBeNull();
        ParameterInfo[] invokeMethodParameters = DelegateType.GetMethod("Invoke")?.GetParameters();

        Tokenizer = tokenizer;
        DefaultStaticType = defaultStaticType;
        FirstArgumentIsDefaultInstance = firstArgumentIsDefaultInstance;
        Stack = new ContextStackElement(null, false);

        if (paramNames.IsNullOrEmptyCollection()) paramNames = null;
        if (paramNames is not null && paramNames.Length != invokeMethodParameters.Length) throw new ArgumentException("paramNames for delegate name overriding must be of the same length than the delegate type argument count", nameof(paramNames));

        Parameters = new List<ParameterExpression>(invokeMethodParameters.Length);

        for (int i = 0; i < invokeMethodParameters.Length; i++)
        {
            Parameters.Add(Expression.Parameter(invokeMethodParameters[i].ParameterType, paramNames?[i] ?? invokeMethodParameters[i].Name));
        }

        if (firstArgumentIsDefaultInstance) DefaultInstanceParam = Parameters.FirstOrDefault();
    }

    public ParserContext(ParameterExpression[] parameters, Type defaultStaticType, Tokenizer tokenizer, bool firstTypeIsDefaultInstance)
    {
        DelegateType = null;
        Tokenizer = tokenizer;
        DefaultStaticType = defaultStaticType;
        FirstArgumentIsDefaultInstance = firstTypeIsDefaultInstance;
        DefaultInstanceParam = firstTypeIsDefaultInstance && parameters.Length > 0 ? parameters[0] : null;
        Stack = new ContextStackElement(null, false);

        // Get the parameter types of the delegate
        foreach (var p in parameters)
        {
            Parameters.Add(p);
        }
    }


    public Type DelegateType { get; }
    public Tokenizer Tokenizer { get; }
    public Type DefaultStaticType { get; }

    public IList<ParameterExpression> Parameters { get; } = new List<ParameterExpression>();

    public bool FirstArgumentIsDefaultInstance { get; }
    public ParameterExpression DefaultInstanceParam { get; }

    private ContextStackElement Stack { get; set; }
    public int Depth => Stack?.Depth ?? -1;
    public ICollection<ParameterExpression> StackVariables => Stack.Variables;

    public LabelTarget ContinueLabel { get { ContextStackElement s; for (s = Stack; s != null && s.ContinueLabel == null; s = s.parent) ; return s?.ContinueLabel; } }
    public LabelTarget BreakLabel { get { ContextStackElement s; for (s = Stack; s != null && s.ContinueLabel == null; s = s.parent) ; return s?.BreakLabel; } }

    public ParameterExpression FirstParameter => Parameters.FirstOrDefault();

    public void PushContext(bool lambdaExpression = false)
    {
        Stack = new ContextStackElement(Stack, lambdaExpression); 
    }

    public void PushContext(LabelTarget continueLabel, LabelTarget breakLabel, bool lambdaExpression = false)
    {
        Stack = new ContextStackElement(Stack, lambdaExpression)
        {
            ContinueLabel = continueLabel,
            BreakLabel = breakLabel
        };
    }


    public bool PopContext()
    {
        Stack = Stack.parent;
        return Stack.parent != null;
    }

    public bool AddLabel(LabelTarget label) => Stack.AddLabel(label);
    public bool[] AddLabels(params LabelTarget[] labels) => AddLabels((IEnumerable<LabelTarget>)labels);
    public bool[] AddLabels(IEnumerable<LabelTarget> labels) => labels.Select(AddLabel).ToArray();

    public bool AddVariable(ParameterExpression variable) => Stack.AddVariable(variable);
    public bool[] AddVariables(params ParameterExpression[] variables) => AddVariables((IEnumerable<ParameterExpression>)variables);
    public bool[] AddVariables(IEnumerable<ParameterExpression> variables) => variables.Select(AddVariable).ToArray();

    public bool TryFindVariable(string name, out ParameterExpression parameter)
    {
        if (Stack.TryFindParameter(name, out parameter)) return true;
        parameter = Parameters.FirstOrDefault(p => p.Name == name);
        return parameter != null;
    }
}

internal class ContextStackElement
{

    internal readonly ContextStackElement parent;

    public bool LambdaExpression { get; }
    public int Depth { get; }

    internal ContextStackElement(ContextStackElement parent, bool lambdaExpression)
    {
        this.parent = parent;
        Depth = parent == null ? 0 : parent.Depth + 1;
        LambdaExpression = lambdaExpression;
    }

    private readonly IDictionary<string, ParameterExpression> variables = new Dictionary<string, ParameterExpression>();
    public ICollection<ParameterExpression> Variables => variables.Values;

    private readonly IDictionary<string, LabelTarget> labels = new Dictionary<string, LabelTarget>();
    public ICollection<LabelTarget> Labels => labels.Values;

    public LabelTarget ContinueLabel { get; init; } = null;
    public LabelTarget BreakLabel { get; init; } = null;

    public ParameterExpression FirstParameter => parent?.FirstParameter ?? parent.variables.FirstOrDefault().Value;

    internal bool AddLabel(LabelTarget label)
    {
        if (TryFindLabel(label.Name, out var _)) return false;
        labels.Add(label.Name, label);
        return true;
    }

    internal bool TryFindLabel(string name, out LabelTarget label)
    {
        if (labels.TryGetValue(name, out label)) return true;
        if (parent != null && parent.TryFindLabel(name, out label)) return true;
        label = null;
        return false;
    }


    internal bool AddVariable(ParameterExpression variable)
    {
        if (TryFindParameter(variable.Name, out _)) return false;
        variables.Add(variable.Name, variable);
        return true;
    }

    internal bool TryFindParameter(string name, out ParameterExpression parameter)
    {
        if (variables.TryGetValue(name, out parameter)) return true;
        if (parent != null && parent.TryFindParameter(name, out parameter)) return true;
        parameter = null;
        return false;
    }

}
