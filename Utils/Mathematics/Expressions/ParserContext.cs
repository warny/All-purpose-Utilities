using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace Utils.Expressions;

public class ParserContext
{
    public ParserContext(Type delegateType, Tokenizer tokenizer, Type defaultInstanceType, Type[] paramTypes, bool firstTypeIsDefaultInstance)
    {
        DelegateType = delegateType;
        Tokenizer = tokenizer;
        DefaultInstanceType = defaultInstanceType;
        ParamTypes = paramTypes;
        FirstTypeIsDefaultInstance = firstTypeIsDefaultInstance;
        DefaultInstanceParam = defaultInstanceType != null ? Expression.Parameter(defaultInstanceType, "___DefaultInstanceParam") : null;
        stack = new ContextStackElement(null);

        ParameterInfo[] invokeMethodParameters = DelegateType.GetMethod("Invoke")?.GetParameters();

        // Get the parameter types of the delegate
        ParamTypes ??= invokeMethodParameters?.Select(m => m.ParameterType).ToArray();

        // Determine whether a specific delegate type is specified
        if (invokeMethodParameters != null && FirstTypeIsDefaultInstance && DelegateType.GetType().GetTypeInfo().IsAssignableFrom(typeof(MulticastDelegate).GetTypeInfo()))
        {
            ParameterInfo firstParam = invokeMethodParameters.FirstOrDefault();
            if (firstParam != null)
            {
                DefaultInstanceType = firstParam.ParameterType;
            }
        }

        if (DefaultInstanceParam != null)
        {
            // Add default parameter
            Parameters.Add(DefaultInstanceParam);
        }

    }

    public Type DelegateType { get; }
    public Tokenizer Tokenizer { get; }
    public Type DefaultInstanceType { get; }

    public Type[] ParamTypes { get; set; }
    public IList<ParameterExpression> Parameters { get; } = new List<ParameterExpression>();

    public bool FirstTypeIsDefaultInstance { get; }
    public ParameterExpression DefaultInstanceParam { get; }

    private ContextStackElement stack { get; set; }
    public int Depth => stack?.Depth ?? -1;
    public ICollection<ParameterExpression> StackVariables => stack.Variables;

    public LabelTarget ContinueLabel { get { ContextStackElement s; for (s = stack; s != null && s.ContinueLabel == null; s = s.parent) ; return s?.ContinueLabel; } }
    public LabelTarget BreakLabel { get { ContextStackElement s; for (s = stack; s != null && s.ContinueLabel == null; s = s.parent) ; return s?.BreakLabel; } }

    public ParameterExpression FirstParameter => Parameters.FirstOrDefault();

    public void PushContext()
    {
        stack = new ContextStackElement(stack); 
    }

    public void PushContext(LabelTarget continueLabel, LabelTarget breakLabel)
    {
        stack = new ContextStackElement(stack)
        {
            ContinueLabel = continueLabel,
            BreakLabel = breakLabel
        };
    }


    public bool PopContext()
    {
        stack = stack.parent;
        return stack.parent != null;
    }

    public bool AddLabel(LabelTarget label) => stack.AddLabel(label);
    public bool[] AddLabels(params LabelTarget[] labels) => AddLabels((IEnumerable<LabelTarget>)labels);
    public bool[] AddLabels(IEnumerable<LabelTarget> labels) => labels.Select(AddLabel).ToArray();

    public bool AddVariable(ParameterExpression variable) => stack.AddVariable(variable);
    public bool[] AddVariables(params ParameterExpression[] variables) => AddVariables((IEnumerable<ParameterExpression>)variables);
    public bool[] AddVariables(IEnumerable<ParameterExpression> variables) => variables.Select(AddVariable).ToArray();

    public bool TryFindVariable(string name, out ParameterExpression parameter)
    {
        if (stack.TryFindParameter(name, out parameter)) return true;
        parameter = Parameters.FirstOrDefault(p => p.Name == name);
        return parameter != null;
    }
}

internal class ContextStackElement
{

    internal ContextStackElement parent;

    public int Depth { get; }

    internal ContextStackElement(ContextStackElement parent)
    {
        this.parent = parent;
        Depth = parent == null ? 0 : parent.Depth + 1;
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
