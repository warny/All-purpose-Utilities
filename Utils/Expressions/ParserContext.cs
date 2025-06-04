using System.Linq.Expressions;
using System.Reflection;
using Utils.Objects;

namespace Utils.Expressions;

/// <summary>
/// Maintains contextual information while parsing expressions.
/// </summary>
public class ParserContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="ParserContext"/> using a delegate signature.
    /// </summary>
    /// <param name="delegateType">Delegate describing the lambda signature.</param>
    /// <param name="paramNames">Optional parameter names overriding the delegate.</param>
    /// <param name="defaultStaticType">Type used for static member resolution when no instance is provided.</param>
    /// <param name="tokenizer">Tokenizer providing the input tokens.</param>
    /// <param name="firstArgumentIsDefaultInstance">Whether the first argument acts as the default instance.</param>
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

    /// <summary>
    /// Initializes a new instance of <see cref="ParserContext"/> using explicit parameters.
    /// </summary>
    /// <param name="parameters">Parameters representing the lambda signature.</param>
    /// <param name="defaultStaticType">Type used for static member resolution when no instance is provided.</param>
    /// <param name="tokenizer">Tokenizer providing the input tokens.</param>
    /// <param name="firstTypeIsDefaultInstance">Whether the first parameter should be used as the default instance.</param>
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


    /// <summary>
    /// Gets the delegate type used to infer parameter information when available.
    /// </summary>
    public Type DelegateType { get; }

    /// <summary>
    /// Gets the tokenizer supplying tokens for the parser.
    /// </summary>
    public Tokenizer Tokenizer { get; }

    /// <summary>
    /// Gets the type used for resolving static members when no instance is provided.
    /// </summary>
    public Type DefaultStaticType { get; }

    /// <summary>
    /// Gets the collection of parameters available in the current parsing context.
    /// </summary>
    public IList<ParameterExpression> Parameters { get; } = new List<ParameterExpression>();

    /// <summary>
    /// Gets a value indicating whether the first argument should be used as the default instance.
    /// </summary>
    public bool FirstArgumentIsDefaultInstance { get; }

    /// <summary>
    /// Gets the parameter expression representing the default instance when available.
    /// </summary>
    public ParameterExpression DefaultInstanceParam { get; }

    private ContextStackElement Stack { get; set; }

    /// <summary>
    /// Gets the current depth of the context stack.
    /// </summary>
    public int Depth => Stack?.Depth ?? -1;

    /// <summary>
    /// Gets the variables defined in the current stack frame.
    /// </summary>
    public ICollection<ParameterExpression> StackVariables => Stack.Variables;

    /// <summary>
    /// Gets the nearest continue label in the stack if any.
    /// </summary>
    public LabelTarget ContinueLabel { get { ContextStackElement s; for (s = Stack; s != null && s.ContinueLabel == null; s = s.parent) ; return s?.ContinueLabel; } }

    /// <summary>
    /// Gets the nearest break label in the stack if any.
    /// </summary>
    public LabelTarget BreakLabel { get { ContextStackElement s; for (s = Stack; s != null && s.ContinueLabel == null; s = s.parent) ; return s?.BreakLabel; } }

    /// <summary>
    /// Gets the first parameter of the context or <c>null</c> if none.
    /// </summary>
    public ParameterExpression FirstParameter => Parameters.FirstOrDefault();

    /// <summary>
    /// Pushes a new context frame onto the stack.
    /// </summary>
    /// <param name="lambdaExpression">Indicates whether the frame is for a lambda body.</param>
    public void PushContext(bool lambdaExpression = false)
    {
        Stack = new ContextStackElement(Stack, lambdaExpression);
    }

    /// <summary>
    /// Pushes a new context frame with loop labels.
    /// </summary>
    /// <param name="continueLabel">Label used for continue statements.</param>
    /// <param name="breakLabel">Label used for break statements.</param>
    /// <param name="lambdaExpression">Indicates whether the frame is for a lambda body.</param>
    public void PushContext(LabelTarget continueLabel, LabelTarget breakLabel, bool lambdaExpression = false)
    {
        Stack = new ContextStackElement(Stack, lambdaExpression)
        {
            ContinueLabel = continueLabel,
            BreakLabel = breakLabel
        };
    }


    /// <summary>
    /// Pops the current context frame.
    /// </summary>
    /// <returns><c>true</c> if another frame remains on the stack.</returns>
    public bool PopContext()
    {
        Stack = Stack.parent;
        return Stack.parent != null;
    }

    /// <summary>
    /// Adds a label to the current context frame.
    /// </summary>
    public bool AddLabel(LabelTarget label) => Stack.AddLabel(label);

    /// <summary>
    /// Adds multiple labels to the current context frame.
    /// </summary>
    public bool[] AddLabels(params LabelTarget[] labels) => AddLabels((IEnumerable<LabelTarget>)labels);
    public bool[] AddLabels(IEnumerable<LabelTarget> labels) => labels.Select(AddLabel).ToArray();

    /// <summary>
    /// Adds a variable to the current context frame.
    /// </summary>
    public bool AddVariable(ParameterExpression variable) => Stack.AddVariable(variable);

    /// <summary>
    /// Adds multiple variables to the current context frame.
    /// </summary>
    public bool[] AddVariables(params ParameterExpression[] variables) => AddVariables((IEnumerable<ParameterExpression>)variables);
    public bool[] AddVariables(IEnumerable<ParameterExpression> variables) => variables.Select(AddVariable).ToArray();

    /// <summary>
    /// Attempts to find a variable in the current context stack.
    /// </summary>
    /// <param name="name">Name of the variable.</param>
    /// <param name="parameter">Returns the parameter expression if found.</param>
    /// <returns><c>true</c> if the variable exists.</returns>
    public bool TryFindVariable(string name, out ParameterExpression parameter)
    {
        if (Stack.TryFindParameter(name, out parameter)) return true;
        parameter = Parameters.FirstOrDefault(p => p.Name == name);
        return parameter != null;
    }
}

/// <summary>
/// Represents a frame in the parser context stack.
/// </summary>
internal class ContextStackElement
{

    internal readonly ContextStackElement parent;

    /// <summary>
    /// Gets a value indicating whether this frame represents a lambda body.
    /// </summary>
    public bool LambdaExpression { get; }

    /// <summary>
    /// Gets the depth of this frame in the stack.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Creates a new stack element.
    /// </summary>
    /// <param name="parent">Parent frame.</param>
    /// <param name="lambdaExpression">Indicates whether this frame represents a lambda body.</param>
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

    /// <summary>
    /// Registers a label within the current frame.
    /// </summary>
    internal bool AddLabel(LabelTarget label)
    {
        if (TryFindLabel(label.Name, out var _)) return false;
        labels.Add(label.Name, label);
        return true;
    }

    /// <summary>
    /// Attempts to find a label by name.
    /// </summary>
    internal bool TryFindLabel(string name, out LabelTarget label)
    {
        if (labels.TryGetValue(name, out label)) return true;
        if (parent != null && parent.TryFindLabel(name, out label)) return true;
        label = null;
        return false;
    }


    /// <summary>
    /// Adds a variable to this frame.
    /// </summary>
    internal bool AddVariable(ParameterExpression variable)
    {
        if (TryFindParameter(variable.Name, out _)) return false;
        variables.Add(variable.Name, variable);
        return true;
    }

    /// <summary>
    /// Searches for a variable by name in this frame or its parents.
    /// </summary>
    internal bool TryFindParameter(string name, out ParameterExpression parameter)
    {
        if (variables.TryGetValue(name, out parameter)) return true;
        if (parent != null && parent.TryFindParameter(name, out parameter)) return true;
        parameter = null;
        return false;
    }

}
