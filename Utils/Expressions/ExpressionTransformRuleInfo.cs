using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Expressions;

/// <summary>
/// Identifies one rule of a transformer (a method marked with <see cref="ExpressionSignatureAttribute"/>) in trace events.
/// This type only holds data.
/// </summary>
/// <remarks>
/// One instance exists per rule and per concrete transformer type: it is built once with the transformer's dispatch plan and
/// shared by every event, so tracing never reads reflection metadata per transformation. <see cref="Id"/> is stable for a given
/// build of the transformer type; <see cref="DisplayName"/> is the method name, completed with the parameter types when the
/// type declares several rules of that name.
/// </remarks>
public sealed class ExpressionTransformRuleInfo
{
    /// <summary>The parameter types used by <see cref="Id"/> and, for an overloaded name, by <see cref="DisplayName"/>.</summary>
    private readonly string _parameterList;

    /// <summary>Initializes the identity of a rule.</summary>
    /// <param name="method">The rule method.</param>
    /// <param name="transformerType">The concrete transformer type whose plan holds the rule.</param>
    /// <param name="declaredNodeType">The node type declared by the rule's signature attribute (<c>(ExpressionType)(-1)</c> for every node type).</param>
    /// <param name="declarationIndex">The position of the rule in the plan's declaration order.</param>
    /// <param name="isOverloaded">Whether another rule of the same plan has the same method name.</param>
    internal ExpressionTransformRuleInfo(MethodInfo method, Type transformerType, ExpressionType declaredNodeType, int declarationIndex, bool isOverloaded)
    {
        Method = method;
        TransformerType = transformerType;
        DeclaredNodeType = declaredNodeType;
        DeclarationIndex = declarationIndex;
        _parameterList = string.Join(", ", method.GetParameters().Select(static parameter => parameter.ParameterType.Name));
        DisplayName = isOverloaded ? $"{method.Name}({_parameterList})" : method.Name;
        Id = $"{method.DeclaringType?.FullName}.{method.Name}({_parameterList})";
    }

    /// <summary>Gets the rule method.</summary>
    public MethodInfo Method { get; }

    /// <summary>Gets the name of the rule method.</summary>
    public string Name => Method.Name;

    /// <summary>Gets the concrete transformer type whose dispatch plan holds the rule (the method may be declared by a base type).</summary>
    public Type TransformerType { get; }

    /// <summary>Gets the node type declared by the rule's signature attribute, or <c>(ExpressionType)(-1)</c> for a rule declared on every node type.</summary>
    public ExpressionType DeclaredNodeType { get; }

    /// <summary>Gets the position of the rule in the declaration order of the plan (the order in which candidates are tried).</summary>
    public int DeclarationIndex { get; }

    /// <summary>Gets the short name of the rule: the method name, with its parameter types when the name is overloaded.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the stable identifier of the rule: declaring type, method name and parameter types.</summary>
    public string Id { get; }

    /// <summary>Returns <see cref="Id"/>.</summary>
    /// <returns>The stable identifier of the rule.</returns>
    public override string ToString() => Id;
}
