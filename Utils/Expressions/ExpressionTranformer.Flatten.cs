using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Utils.Expressions;

/// <summary>
/// The <see cref="FlattenAttribute"/> part of <see cref="ExpressionTransformer"/>: discovery and validation of the flatten rules,
/// iterative extraction of an arithmetic chain from the raw tree, invocation of the rules, and the historical fallback when
/// every rule declines (see <c>docs/expressions/FlattenArchitecture.md</c>).
/// </summary>
public abstract partial class ExpressionTransformer
{
    /// <summary>
    /// Gets the number of operands of the framework node that carries <paramref name="nodeType"/>: 2 for the node types of
    /// <see cref="BinaryExpression"/>, 1 for those of <see cref="UnaryExpression"/>, 0 for every other one. This is the shape
    /// of the .NET nodes, used to validate the declarations; it gives no role to any operator.
    /// </summary>
    /// <param name="nodeType">A declared node type.</param>
    /// <returns>The number of operands, or 0.</returns>
    private static int FlattenNodeArity(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Add or ExpressionType.AddChecked or ExpressionType.And or ExpressionType.AndAlso
            or ExpressionType.ArrayIndex or ExpressionType.Coalesce or ExpressionType.Divide or ExpressionType.Equal
            or ExpressionType.ExclusiveOr or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
            or ExpressionType.LeftShift or ExpressionType.LessThan or ExpressionType.LessThanOrEqual or ExpressionType.Modulo
            or ExpressionType.Multiply or ExpressionType.MultiplyChecked or ExpressionType.NotEqual or ExpressionType.Or
            or ExpressionType.OrElse or ExpressionType.Power or ExpressionType.RightShift or ExpressionType.Subtract
            or ExpressionType.SubtractChecked or ExpressionType.Assign or ExpressionType.AddAssign
            or ExpressionType.AndAssign or ExpressionType.DivideAssign or ExpressionType.ExclusiveOrAssign
            or ExpressionType.LeftShiftAssign or ExpressionType.ModuloAssign or ExpressionType.MultiplyAssign
            or ExpressionType.OrAssign or ExpressionType.PowerAssign or ExpressionType.RightShiftAssign
            or ExpressionType.SubtractAssign or ExpressionType.AddAssignChecked or ExpressionType.MultiplyAssignChecked
            or ExpressionType.SubtractAssignChecked => 2,
        ExpressionType.ArrayLength or ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.Negate
            or ExpressionType.NegateChecked or ExpressionType.Not or ExpressionType.Quote or ExpressionType.TypeAs
            or ExpressionType.UnaryPlus or ExpressionType.Decrement or ExpressionType.Increment or ExpressionType.IsFalse
            or ExpressionType.IsTrue or ExpressionType.Throw or ExpressionType.Unbox or ExpressionType.OnesComplement
            or ExpressionType.PreIncrementAssign or ExpressionType.PreDecrementAssign or ExpressionType.PostIncrementAssign
            or ExpressionType.PostDecrementAssign => 1,
        _ => 0,
    };

    /// <summary>
    /// One couple declared by flatten rules, and the rules that declare it, in declaration order: a direct operation, an
    /// optional inverse operation, and the unary operators attached to it, bound to the couple itself
    /// (<see cref="UnaryOperatorBinding.FirstGroup"/>) or through their operand (<see cref="UnaryOperatorBinding.OperandGroup"/>).
    /// A binary operator belongs to at most one group, so a chain is extracted once whatever the number of rules that receive
    /// it. Immutable once the plan is built.
    /// </summary>
    private sealed class FlattenGroup
    {
        /// <summary>Initializes a group.</summary>
        /// <param name="direct">The direct operation.</param>
        /// <param name="inverse">The inverse operation, or <see langword="null"/>.</param>
        /// <param name="ownUnaries">The unary operators bound to the couple itself.</param>
        /// <param name="operandUnaries">The unary operators bound to the couple through their operand.</param>
        /// <param name="rules">The rules, in declaration order.</param>
        public FlattenGroup(ExpressionType direct, ExpressionType? inverse, ExpressionType[] ownUnaries, ExpressionType[] operandUnaries, TransformRule[] rules)
        {
            Direct = direct;
            Inverse = inverse;
            OwnUnaries = ownUnaries;
            OperandUnaries = operandUnaries;
            Rules = rules;
        }

        /// <summary>Gets the direct operation: the operation of a term reached by the initial context.</summary>
        public ExpressionType Direct { get; }

        /// <summary>Gets the inverse operation (the operation of a term reached by the inverted context), or <see langword="null"/>.</summary>
        public ExpressionType? Inverse { get; }

        /// <summary>Gets the unary operators bound to the couple itself (<see cref="UnaryOperatorBinding.FirstGroup"/>).</summary>
        public ExpressionType[] OwnUnaries { get; }

        /// <summary>Gets the unary operators bound to the couple through their operand (<see cref="UnaryOperatorBinding.OperandGroup"/>).</summary>
        public ExpressionType[] OperandUnaries { get; }

        /// <summary>Gets the rules of the group, in declaration order.</summary>
        public TransformRule[] Rules { get; }

        /// <summary>Gets the operation of a term reached by the given context.</summary>
        /// <param name="inverted">Whether the context is inverted; only possible when the group has an inverse operation.</param>
        /// <returns>The operation of the term.</returns>
        public ExpressionType OperationOf(bool inverted) => inverted ? Inverse!.Value : Direct;
    }

    /// <summary>
    /// The declared behavior of one operator node type: its number of operands, how it propagates the context to each of them,
    /// and the group its nodes belong to. A binary operator, and a unary operator bound with
    /// <see cref="UnaryOperatorBinding.FirstGroup"/>, belong to one group by themselves; a unary operator bound with
    /// <see cref="UnaryOperatorBinding.OperandGroup"/> belongs to the group of its operand, among the groups that declare it.
    /// Immutable once the plan is built.
    /// </summary>
    private sealed class FlattenOperator
    {
        /// <summary>Initializes a descriptor.</summary>
        /// <param name="arity">The number of operands: 1 or 2.</param>
        /// <param name="invertedOperands">The operands whose context is inverted, as a bit set (bit <c>i</c> for operand <c>i</c>).</param>
        /// <param name="group">The group of the operator by itself, or <see langword="null"/> when its operand decides it.</param>
        /// <param name="operandGroups">The groups that declare it through its operand; empty when <paramref name="group"/> is set.</param>
        public FlattenOperator(int arity, int invertedOperands, FlattenGroup? group, FlattenGroup[] operandGroups)
        {
            Arity = arity;
            InvertedOperands = invertedOperands;
            Group = group;
            OperandGroups = operandGroups;
        }

        /// <summary>Gets the number of operands: 1 or 2.</summary>
        public int Arity { get; }

        /// <summary>Gets the operands whose context is inverted, as a bit set.</summary>
        public int InvertedOperands { get; }

        /// <summary>Gets the group the operator belongs to by itself, or <see langword="null"/> when its operand decides it.</summary>
        public FlattenGroup? Group { get; }

        /// <summary>Gets the groups that declare the operator through its operand (<see cref="UnaryOperatorBinding.OperandGroup"/>).</summary>
        public FlattenGroup[] OperandGroups { get; }

        /// <summary>Gets the context of operand <paramref name="operand"/> from the context of the node.</summary>
        /// <param name="inverted">Whether the context of the node is inverted.</param>
        /// <param name="operand">The index of the operand.</param>
        /// <returns>Whether the context of the operand is inverted.</returns>
        public bool OperandContext(bool inverted, int operand) => inverted ^ (((InvertedOperands >> operand) & 1) != 0);

        /// <summary>Whether <paramref name="group"/> declares the operator through its operand.</summary>
        /// <param name="group">A group of the plan.</param>
        /// <returns><see langword="true"/> when the operator joins <paramref name="group"/> when its operand belongs to it.</returns>
        public bool AcceptsOperandGroup(FlattenGroup group)
        {
            foreach (FlattenGroup candidate in OperandGroups)
            {
                if (ReferenceEquals(candidate, group)) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Builds the operator index of <see cref="TransformPlan"/> from the validated groups: the direct operation passes the
    /// context to both operands, the inverse operation inverts it for its right operand, a unary operator inverts it for its
    /// operand.
    /// </summary>
    /// <param name="groups">The validated groups (a unary operator bound to its group by itself belongs to that group only).</param>
    /// <returns>The descriptor of each declared operator, indexed by node type value; empty without groups.</returns>
    private static FlattenOperator?[] BuildFlattenOperatorIndex(FlattenGroup[] groups)
    {
        if (groups.Length == 0) return [];

        var index = new FlattenOperator?[(int)_allExpressionTypes.Max() + 1];
        var operandGroups = new Dictionary<ExpressionType, List<FlattenGroup>>();
        foreach (FlattenGroup group in groups)
        {
            index[(int)group.Direct] = new FlattenOperator(2, 0b00, group, []);
            if (group.Inverse is ExpressionType inverse) index[(int)inverse] = new FlattenOperator(2, 0b10, group, []);
            foreach (ExpressionType unary in group.OwnUnaries) index[(int)unary] = new FlattenOperator(1, 0b1, group, []);
            foreach (ExpressionType unary in group.OperandUnaries)
            {
                if (!operandGroups.TryGetValue(unary, out List<FlattenGroup>? owners)) operandGroups.Add(unary, owners = []);
                owners.Add(group);
            }
        }

        foreach ((ExpressionType unary, List<FlattenGroup> owners) in operandGroups)
        {
            index[(int)unary] = new FlattenOperator(1, 0b1, null, [.. owners]);
        }

        return index;
    }

    /// <summary>Creates the exception reported for an invalid <see cref="FlattenAttribute"/> rule, when the plan is built.</summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="method">The rule method.</param>
    /// <param name="reason">Why the rule is invalid.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidOperationException InvalidFlattenRule(Type transformerType, MethodInfo method, string reason)
        => new($"The [Flatten] rule '{method.DeclaringType?.FullName}.{method.Name}' of transformer '{transformerType.FullName}' is invalid: {reason}.");

    /// <summary>
    /// Rejects a static method marked with <see cref="FlattenAttribute"/> or <see cref="FlattenUnaryAttribute"/>: the plan only
    /// holds instance rules, so such a method would otherwise be silently ignored.
    /// </summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <exception cref="InvalidOperationException">A static method is marked with a flatten attribute.</exception>
    private static void ThrowOnStaticFlattenRule(Type transformerType)
    {
        foreach (MethodInfo method in transformerType.GetMethods(Public | NonPublic | Static | FlattenHierarchy))
        {
            if (method.IsDefined(typeof(FlattenAttribute), inherit: true) || method.IsDefined(typeof(FlattenUnaryAttribute), inherit: true))
            {
                throw InvalidFlattenRule(transformerType, method, "a flatten rule must be an instance method");
            }
        }
    }

    /// <summary>The reason given for a <see cref="FlattenUnaryAttribute"/> on a method declaration without a <see cref="FlattenAttribute"/>.</summary>
    private const string OrphanFlattenUnaryReason = "it is marked with FlattenUnaryAttribute without a FlattenAttribute on the same declaration";

    /// <summary>One unary operator of a flatten declaration and its binding. Data only.</summary>
    /// <param name="Operation">The node type of the unary operator.</param>
    /// <param name="Binding">How it is attached to the couples of the declaration.</param>
    private readonly record struct FlattenUnaryDeclaration(ExpressionType Operation, UnaryOperatorBinding Binding);

    /// <summary>
    /// Reads the unary operators of the effective <see cref="FlattenAttribute"/> of <paramref name="method"/>: the operator of
    /// its simplified constructor, then the <see cref="FlattenUnaryAttribute"/> of the same method declaration, ordered by node
    /// type value (the metadata order of repeated attributes is not specified). The override levels are walked from the most
    /// derived one: the first that declares a <see cref="FlattenAttribute"/> is the effective declaration, and the unary
    /// operators of other levels are never merged into it. A level that declares unary operators without a
    /// <see cref="FlattenAttribute"/> is rejected.
    /// </summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="method">The rule method, as returned by <c>GetMethods</c> (its most derived override).</param>
    /// <param name="attribute">The effective attribute of <paramref name="method"/>.</param>
    /// <returns>The unary operators of the effective declaration.</returns>
    /// <exception cref="InvalidOperationException">A level declares unary operators without a <see cref="FlattenAttribute"/>.</exception>
    private static FlattenUnaryDeclaration[] ReadFlattenUnaries(Type transformerType, MethodInfo method, FlattenAttribute attribute)
    {
        FlattenUnaryAttribute[]? effective = null;
        for (MethodInfo? level = method; level is not null; level = ParentOverride(level))
        {
            FlattenUnaryAttribute[] unaries = [.. level.GetCustomAttributes<FlattenUnaryAttribute>(inherit: false)];
            if (level.IsDefined(typeof(FlattenAttribute), inherit: false))
            {
                effective ??= unaries;
            }
            else if (unaries.Length != 0)
            {
                throw InvalidFlattenRule(transformerType, level, OrphanFlattenUnaryReason);
            }
        }

        var declarations = new List<FlattenUnaryDeclaration>();
        if (attribute.UnaryOperator is ExpressionType simplified) declarations.Add(new FlattenUnaryDeclaration(simplified, UnaryOperatorBinding.FirstGroup));
        declarations.AddRange((effective ?? []).Select(static unary => new FlattenUnaryDeclaration(unary.Operation, unary.Binding)).OrderBy(static unary => (int)unary.Operation));
        return [.. declarations];
    }

    /// <summary>
    /// Gets the method <paramref name="method"/> directly overrides: the nearest declaration of a base type with the same name,
    /// the same parameter types and the same base definition. Reflection only exposes the base definition, so the levels in
    /// between are found by walking the base types.
    /// </summary>
    /// <param name="method">An instance method.</param>
    /// <returns>The overridden method, or <see langword="null"/> when <paramref name="method"/> overrides nothing.</returns>
    private static MethodInfo? ParentOverride(MethodInfo method)
    {
        if (!method.IsVirtual) return null;

        MethodInfo definition = method.GetBaseDefinition();
        if (definition.HasSameMetadataDefinitionAs(method)) return null;

        Type[] parameterTypes = [.. method.GetParameters().Select(static parameter => parameter.ParameterType)];
        for (Type? type = method.DeclaringType?.BaseType; type is not null; type = type.BaseType)
        {
            MethodInfo? candidate = type.GetMethod(method.Name, DeclaredOnly | Public | NonPublic | Instance, null, parameterTypes, null);
            if (candidate is not null && candidate.IsVirtual && candidate.GetBaseDefinition().HasSameMetadataDefinitionAs(definition)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Validates the <see cref="FlattenAttribute"/> rules of a transformer type and groups them by composition (see
    /// <see cref="FlattenGroup"/>): a rule joins the group of each couple it declares. Every check runs once, here, so an
    /// invalid rule fails the construction of the transformer instead of an invocation.
    /// </summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="methods">The instance methods marked with <see cref="FlattenAttribute"/>, in <c>GetMethods</c> order.</param>
    /// <param name="rules">The rules built for <paramref name="methods"/>, in the same order.</param>
    /// <param name="operations">The declared operators of each rule, in the same order (its trace identity).</param>
    /// <returns>The groups, in the order of their first rule.</returns>
    /// <exception cref="InvalidOperationException">
    /// A rule is invalid, two rules share an operator without declaring the same couple, or a unary operator could root the
    /// chains of several couples.
    /// </exception>
    private static FlattenGroup[] BuildFlattenGroups(Type transformerType, List<MethodInfo> methods, out TransformRule[] rules, out IReadOnlyList<ExpressionType>[] operations)
    {
        rules = new TransformRule[methods.Count];
        operations = new IReadOnlyList<ExpressionType>[methods.Count];
        var groups = new List<(ExpressionType Direct, ExpressionType? Inverse, ExpressionType[] Own, ExpressionType[] Operand, List<TransformRule> Rules)>();
        var binaryOwners = new HashSet<ExpressionType>();
        // For each unary operator: whether a group holds it by itself (it then roots that group's chains whatever its operand).
        var unaryOwners = new Dictionary<ExpressionType, bool>();

        for (int i = 0; i < methods.Count; i++)
        {
            MethodInfo method = methods[i];
            FlattenAttribute attribute = method.GetCustomAttributes<FlattenAttribute>(inherit: true).First();
            FlattenUnaryDeclaration[] unaries = ReadFlattenUnaries(transformerType, method, attribute);
            ValidateFlattenDeclaration(transformerType, method, attribute, unaries);
            TransformRule rule = BuildFlattenRule(transformerType, method);
            rules[i] = rule;
            operations[i] = attribute.Operations
                .Concat(unaries.Where(unary => unary.Operation != attribute.UnaryOperator).Select(static unary => unary.Operation))
                .ToList()
                .AsReadOnly();

            ExpressionType[] own = [.. unaries.Where(static unary => unary.Binding == UnaryOperatorBinding.FirstGroup).Select(static unary => unary.Operation)];
            ExpressionType[] operand = [.. unaries.Where(static unary => unary.Binding == UnaryOperatorBinding.OperandGroup).Select(static unary => unary.Operation)];
            for (int couple = 0; couple < attribute.DirectOperations.Count; couple++)
            {
                ExpressionType direct = attribute.DirectOperations[couple];
                ExpressionType? inverse = attribute.InverseOperations.Count == 0 ? null : attribute.InverseOperations[couple];
                ExpressionType[] coupleOwn = couple == 0 ? own : [];
                int group = groups.FindIndex(g => g.Direct == direct && g.Inverse == inverse && SameSet(g.Own, coupleOwn) && SameSet(g.Operand, operand));
                if (group < 0)
                {
                    if (binaryOwners.Contains(direct) || (inverse is ExpressionType declared && binaryOwners.Contains(declared)))
                    {
                        throw InvalidFlattenRule(transformerType, method, "its operators overlap those of another flatten rule without declaring the same couple; rules sharing a binary operator must declare the same direct operation, inverse operation, and unary operators with the same bindings");
                    }

                    // A new group: a unary operator it holds by itself must belong to no other group, and an operator another
                    // group holds by itself cannot join it through its operand (either way the root of -x would be ambiguous).
                    foreach (ExpressionType unary in coupleOwn)
                    {
                        if (unaryOwners.ContainsKey(unary)) throw AmbiguousFlattenUnary(transformerType, method, unary);
                        unaryOwners.Add(unary, true);
                    }

                    foreach (ExpressionType unary in operand)
                    {
                        if (unaryOwners.TryGetValue(unary, out bool heldByItself) && heldByItself) throw AmbiguousFlattenUnary(transformerType, method, unary);
                        unaryOwners[unary] = false;
                    }

                    groups.Add((direct, inverse, coupleOwn, operand, []));
                    group = groups.Count - 1;
                    binaryOwners.Add(direct);
                    if (inverse is ExpressionType owned) binaryOwners.Add(owned);
                }

                groups[group].Rules.Add(rule);
            }
        }

        return [.. groups.Select(static g => new FlattenGroup(g.Direct, g.Inverse, g.Own, g.Operand, [.. g.Rules]))];
    }

    /// <summary>Creates the exception reported for a unary operator whose dispatch would be ambiguous.</summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="method">The rule that introduces the ambiguity.</param>
    /// <param name="unary">The unary operator.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidOperationException AmbiguousFlattenUnary(Type transformerType, MethodInfo method, ExpressionType unary)
        => InvalidFlattenRule(transformerType, method, $"'{unary}' could root the chains of several couples; a unary operator bound with FirstGroup cannot be declared for another couple (only operators bound with OperandGroup, which their operand attaches to one couple, may be shared)");

    /// <summary>Whether two arrays of distinct operators hold the same operators, whatever their order.</summary>
    /// <param name="left">The first array.</param>
    /// <param name="right">The second array.</param>
    /// <returns><see langword="true"/> when the sets are equal.</returns>
    private static bool SameSet(ExpressionType[] left, ExpressionType[] right)
        => left.Length == right.Length && left.All(right.Contains);

    /// <summary>
    /// Validates the configuration declared by a flatten rule: at least one couple, couples of matching lengths, binary node
    /// types for the direct and inverse operations, unary node types and known bindings for the unary operators (which need an
    /// inverse operation), and no operator declared twice.
    /// </summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="method">The rule method.</param>
    /// <param name="attribute">Its attribute.</param>
    /// <param name="unaries">The unary operators of the declaration (see <see cref="ReadFlattenUnaries"/>).</param>
    /// <exception cref="InvalidOperationException">The declaration is not consistent.</exception>
    private static void ValidateFlattenDeclaration(Type transformerType, MethodInfo method, FlattenAttribute attribute, FlattenUnaryDeclaration[] unaries)
    {
        if (attribute.DirectOperations.Count == 0)
        {
            throw InvalidFlattenRule(transformerType, method, "it declares no operator");
        }

        if (attribute.InverseOperations.Count != 0 && attribute.InverseOperations.Count != attribute.DirectOperations.Count)
        {
            throw InvalidFlattenRule(transformerType, method, $"its {attribute.DirectOperations.Count} direct and {attribute.InverseOperations.Count} inverse operations do not form couples");
        }

        if (attribute.InverseOperations.Count == 0 && unaries.Length != 0)
        {
            throw InvalidFlattenRule(transformerType, method, "it declares unary operators without an inverse operation");
        }

        foreach (ExpressionType operation in attribute.DirectOperations.Concat(attribute.InverseOperations))
        {
            if (FlattenNodeArity(operation) != 2)
            {
                throw InvalidFlattenRule(transformerType, method, $"'{operation}' is not the node type of a BinaryExpression, as a direct or inverse operation must be");
            }
        }

        foreach (FlattenUnaryDeclaration unary in unaries)
        {
            if (FlattenNodeArity(unary.Operation) != 1)
            {
                throw InvalidFlattenRule(transformerType, method, $"'{unary.Operation}' is not the node type of a UnaryExpression, as a unary operator must be");
            }

            if (unary.Binding is not (UnaryOperatorBinding.FirstGroup or UnaryOperatorBinding.OperandGroup))
            {
                throw InvalidFlattenRule(transformerType, method, $"'{unary.Binding}' is not a binding of the unary operator '{unary.Operation}'");
            }
        }

        var declared = new HashSet<ExpressionType>();
        foreach (ExpressionType operation in attribute.DirectOperations.Concat(attribute.InverseOperations).Concat(unaries.Select(static unary => unary.Operation)))
        {
            if (!declared.Add(operation))
            {
                throw InvalidFlattenRule(transformerType, method, $"'{operation}' is declared more than once");
            }
        }
    }

    /// <summary>
    /// Builds the <see cref="TransformRule"/> of a flatten rule after checking its shape:
    /// <c>Expression? Rule(FlattenedTerm[])</c> or <c>Expression? Rule(Expression, FlattenedTerm[])</c>.
    /// </summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="method">The rule method.</param>
    /// <returns>The rule, of kind <see cref="InvocationKind.Flattened"/>.</returns>
    /// <exception cref="InvalidOperationException">The method's shape is not one of the two supported signatures.</exception>
    private static TransformRule BuildFlattenRule(Type transformerType, MethodInfo method)
    {
        if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
        {
            throw InvalidFlattenRule(transformerType, method, "a flatten rule cannot be generic");
        }

        if (!_typeOfExpression.IsAssignableFrom(method.ReturnType))
        {
            throw InvalidFlattenRule(transformerType, method, "a flatten rule must return an Expression");
        }

        ParameterInfo[] parameterInfos = method.GetParameters();
        bool validShape = parameterInfos.Length switch
        {
            1 => parameterInfos[0].ParameterType == typeof(FlattenedTerm[]),
            2 => parameterInfos[0].ParameterType == typeof(Expression) && parameterInfos[1].ParameterType == typeof(FlattenedTerm[]),
            _ => false,
        };
        if (!validShape)
        {
            throw InvalidFlattenRule(transformerType, method, "its parameters must be (FlattenedTerm[] terms) or (Expression source, FlattenedTerm[] terms)");
        }

        var parameters = new TransformParameter[parameterInfos.Length];
        for (int i = 0; i < parameterInfos.Length; i++)
        {
            // A constraint on a flatten parameter would never be evaluated: reject it rather than ignore it. IsDefined reads the
            // attribute data (inherited ones included) without constructing a custom attribute.
            if (Attribute.IsDefined(parameterInfos[i], typeof(ExpressionSignatureAttribute), inherit: true))
            {
                throw InvalidFlattenRule(transformerType, method, "the parameters of a flatten rule cannot carry an ExpressionSignatureAttribute");
            }

            parameters[i] = new TransformParameter(parameterInfos[i].ParameterType, null, null);
        }

        bool fastInvokerEligible = DetermineFastInvokerEligibility(method, parameters, InvocationKind.Flattened);
        return new TransformRule(method, null, parameters, InvocationKind.Flattened, returnsExpression: true, fastInvokerEligible);
    }

    /// <summary>Builds the trace identity of every flatten rule, numbered in their own declaration order.</summary>
    /// <param name="transformerType">The transformer type whose plan is being built.</param>
    /// <param name="rules">The flatten rules, in declaration order.</param>
    /// <param name="operations">The declared operators of each rule, in the same order.</param>
    /// <param name="nameCounts">The number of rules (of both kinds) per method name.</param>
    private static void AssignFlattenRuleInfos(Type transformerType, TransformRule[] rules, IReadOnlyList<ExpressionType>[] operations, Dictionary<string, int> nameCounts)
    {
        for (int i = 0; i < rules.Length; i++)
        {
            TransformRule rule = rules[i];
            rule.Info = new ExpressionTransformRuleInfo(rule.Method, transformerType, operations[i][0], i, nameCounts[rule.Method.Name] > 1, operations[i]);
        }
    }

    /// <summary>
    /// Decides whether an operator node of a declared flatten operator may be crossed by a chain (structural crossing only: it
    /// grants no permission to reassociate, drop or move anything). The default accepts only an ordinary intrinsic operator:
    /// no operator method (so no user-defined or <see langword="decimal"/> operator), and a result of one of the runtime's
    /// primitive arithmetic types (<see langword="int"/>, <see langword="uint"/>, <see langword="long"/>,
    /// <see langword="ulong"/>, <see langword="short"/>, <see langword="ushort"/>, <see langword="float"/>,
    /// <see langword="double"/>), compared by reference, which excludes the lifted (nullable) operators and any user
    /// <see cref="Type"/> object. It reads no node type: which operators a chain crosses is decided by the declarations alone.
    /// </summary>
    /// <param name="node">A framework <see cref="BinaryExpression"/> or <see cref="UnaryExpression"/> whose node type the group declares.</param>
    /// <returns><see langword="true"/> when the chain may cross <paramref name="node"/>; otherwise it is an operand of the chain.</returns>
    /// <remarks>
    /// <c>internal virtual</c>, not <c>protected</c>: only a transformer of this assembly (the simplifier) may widen it. It must
    /// read no member of the node's operands: an operand may be an extension node (ETC R4).
    /// </remarks>
    internal virtual bool CanFlattenThrough(Expression node) => node switch
    {
        BinaryExpression binary => binary.Method is null && IsIntrinsicArithmeticType(binary.Type),
        UnaryExpression unary => unary.Method is null && IsIntrinsicArithmeticType(unary.Type),
        _ => false,
    };

    /// <summary>
    /// Whether <paramref name="type"/> is, by reference, one of the runtime's primitive types with intrinsic arithmetic
    /// operators (see <see cref="CanFlattenThrough"/>). No member of <paramref name="type"/> is read.
    /// </summary>
    /// <param name="type">The result type of an operator node.</param>
    /// <returns><see langword="true"/> for an intrinsic arithmetic type.</returns>
    private static bool IsIntrinsicArithmeticType(Type type)
        => ReferenceEquals(type, typeof(double))
            || ReferenceEquals(type, typeof(int))
            || ReferenceEquals(type, typeof(long))
            || ReferenceEquals(type, typeof(float))
            || ReferenceEquals(type, typeof(uint))
            || ReferenceEquals(type, typeof(ulong))
            || ReferenceEquals(type, typeof(short))
            || ReferenceEquals(type, typeof(ushort));

    /// <summary>
    /// Gets the descriptor of the declared operator <paramref name="node"/> is a node of: a framework
    /// <see cref="BinaryExpression"/> of a declared binary operator, or a framework <see cref="UnaryExpression"/> with an
    /// operand of a declared unary one. Type tests come first, so the node type of an extension node is never read.
    /// </summary>
    /// <param name="node">A node of the raw tree.</param>
    /// <returns>The descriptor, or <see langword="null"/>.</returns>
    private FlattenOperator? DescribeFlattenOperator(Expression node) => node switch
    {
        BinaryExpression binary => _transformPlan.GetFlattenOperator(binary.NodeType) is { Arity: 2 } declared ? declared : null,
        UnaryExpression { Operand: not null } unary => _transformPlan.GetFlattenOperator(unary.NodeType) is { Arity: 1 } declared ? declared : null,
        _ => null,
    };

    /// <summary>
    /// Gets the group <paramref name="node"/> belongs to, from the declarations alone: none unless it is a node of a declared
    /// operator (see <see cref="DescribeFlattenOperator"/>) that <see cref="CanFlattenThrough"/> accepts; then the group of a
    /// binary operator or of a unary operator bound with <see cref="UnaryOperatorBinding.FirstGroup"/>; for a unary operator
    /// bound with <see cref="UnaryOperatorBinding.OperandGroup"/>, the group of its operand, decided the same way, provided that
    /// group declares the operator. A chain of a group crosses exactly the nodes that belong to it; a node that belongs to a
    /// group roots a chain of it.
    /// </summary>
    /// <param name="node">A node of the raw tree.</param>
    /// <param name="declared">The descriptor of <paramref name="node"/>, set whenever the result is not <see langword="null"/>.</param>
    /// <returns>The group, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The prefix of <see cref="UnaryOperatorBinding.OperandGroup"/> operators is walked iteratively, one structural visit per
    /// operand examined; no other node costs more than its own descriptor.
    /// </remarks>
    /// <exception cref="ExpressionResourceLimitException">The structural budget is exhausted.</exception>
    private FlattenGroup? FlattenGroupOf(Expression node, out FlattenOperator? declared)
    {
        declared = DescribeFlattenOperator(node);
        if (declared is null || !CanFlattenThrough(node)) return null;
        if (declared.Group is FlattenGroup own) return own;

        // The first node below the prefix of OperandGroup operators decides the group...
        Expression bottom = ((UnaryExpression)node).Operand;
        FlattenGroup? group;
        while (true)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            FlattenOperator? inner = DescribeFlattenOperator(bottom);
            if (inner is null || !CanFlattenThrough(bottom)) return null;
            if ((group = inner.Group) is not null) break;
            bottom = ((UnaryExpression)bottom).Operand;
        }

        // ...provided every operator of the prefix declares it: otherwise the node belongs to no group.
        for (Expression current = node; !ReferenceEquals(current, bottom); current = ((UnaryExpression)current).Operand)
        {
            if (!DescribeFlattenOperator(current)!.AcceptsOperandGroup(group)) return null;
        }

        return group;
    }

    /// <summary>
    /// Gets the descriptor of <paramref name="node"/> when a chain of <paramref name="group"/> crosses it, that is when the node
    /// belongs to the group (see <see cref="FlattenGroupOf"/>).
    /// </summary>
    /// <param name="node">A node of the raw tree.</param>
    /// <param name="group">The group of the chain.</param>
    /// <returns>The descriptor, or <see langword="null"/> when the node is an operand of the chain.</returns>
    private FlattenOperator? CrossingOperator(Expression node, FlattenGroup group)
        => ReferenceEquals(FlattenGroupOf(node, out FlattenOperator? declared), group) ? declared : null;

    /// <summary>
    /// One node of an extracted chain, in pre-order: a crossed operator node, or an operand. Data only.
    /// </summary>
    /// <param name="Node">The node of the raw tree.</param>
    /// <param name="Crossed">Whether the chain crosses it (otherwise it is an operand).</param>
    private readonly record struct ChainEntry(Expression Node, bool Crossed);

    /// <summary>
    /// An arithmetic chain extracted from the raw tree: the terms given to the rules and the provenance needed to rebuild the
    /// chain faithfully (the crossed nodes, their operators, methods, types and grouping, in pre-order). Built once per
    /// extraction; the terms array is never handed to a rule (each rule receives a copy).
    /// </summary>
    private sealed class FlattenedChain
    {
        /// <summary>
        /// The crossed nodes and operands, in pre-order; <see langword="null"/> when the chain was extracted for rule dispatch
        /// only (the reconstruction extracts its own).
        /// </summary>
        private readonly List<ChainEntry>? _entries;

        /// <summary>Initializes a chain.</summary>
        /// <param name="source">The root of the chain.</param>
        /// <param name="entries">The crossed nodes and operands, in pre-order, when the provenance is kept.</param>
        /// <param name="terms">The terms, in operand order.</param>
        private FlattenedChain(Expression source, List<ChainEntry>? entries, FlattenedTerm[] terms)
        {
            Source = source;
            _entries = entries;
            Terms = terms;
        }

        /// <summary>Gets the root of the chain, as it appears in the raw tree.</summary>
        public Expression Source { get; }

        /// <summary>Gets the terms, in operand order (the pristine copy kept by the engine).</summary>
        public FlattenedTerm[] Terms { get; }

        /// <summary>
        /// Extracts the chain rooted at <paramref name="root"/> with an explicit work stack (no recursion, whatever the depth of
        /// the chain), counting one structural visit per node examined. Every occurrence of a shared node is kept. The walk
        /// only applies the declarations: each crossed node passes to each operand the context its descriptor states, and each
        /// operand becomes a term carrying the operation of the context that reaches it.
        /// </summary>
        /// <param name="transformer">The transformer, which decides which operator nodes are crossed.</param>
        /// <param name="root">The root of the chain; the caller checked that it is crossed.</param>
        /// <param name="rootOperator">The descriptor of <paramref name="root"/>.</param>
        /// <param name="group">The group of the chain.</param>
        /// <param name="keepProvenance">Whether the grouping is recorded, as <see cref="Rebuild"/> requires.</param>
        /// <returns>The extracted chain.</returns>
        /// <exception cref="ExpressionResourceLimitException">The structural budget is exhausted.</exception>
        internal static FlattenedChain Extract(ExpressionTransformer transformer, Expression root, FlattenOperator rootOperator, FlattenGroup group, bool keepProvenance)
        {
            List<ChainEntry>? entries = keepProvenance ? [] : null;
            var terms = new List<FlattenedTerm>();
            // Known: the node is the operand of a crossed OperandGroup operator, whose group was decided by this very operand, so
            // it belongs to the chain without walking its own prefix again.
            var work = new Stack<(Expression Node, bool Inverted, bool Known)>();
            work.Push((root, false, false));

            while (work.Count > 0)
            {
                (Expression node, bool inverted, bool known) = work.Pop();
                ExpressionResourceBudget.ConsumeStructuralVisit();

                FlattenOperator? crossed = ReferenceEquals(node, root) ? rootOperator
                    : known ? transformer.DescribeFlattenOperator(node)
                    : transformer.CrossingOperator(node, group);
                if (crossed is null)
                {
                    entries?.Add(new ChainEntry(node, Crossed: false));
                    terms.Add(new FlattenedTerm(group.OperationOf(inverted), node, terms.Count));
                    continue;
                }

                entries?.Add(new ChainEntry(node, Crossed: true));
                if (crossed.Arity == 1)
                {
                    work.Push((((UnaryExpression)node).Operand, crossed.OperandContext(inverted, 0), crossed.Group is null));
                    continue;
                }

                // The right operand is pushed first so that the left one is examined first (pre-order, left to right).
                BinaryExpression binary = (BinaryExpression)node;
                work.Push((binary.Right, crossed.OperandContext(inverted, 1), false));
                work.Push((binary.Left, crossed.OperandContext(inverted, 0), false));
            }

            return new FlattenedChain(root, entries, terms.ToArray());
        }

        /// <summary>
        /// Rebuilds the chain with the given operands, in operand order: the same operator nodes, with their node types,
        /// methods, lifting and result types, and the same grouping (the very parentheses of the source). Iterative.
        /// </summary>
        /// <param name="operands">One operand per term, in <see cref="FlattenedTerm.SourceIndex"/> order.</param>
        /// <returns><see cref="Source"/> itself when every operand is the original one; otherwise the rebuilt chain.</returns>
        /// <exception cref="ArgumentException">The operand count differs from the term count.</exception>
        /// <exception cref="InvalidOperationException">An operand's type does not fit its operator (thrown by the expression factories).</exception>
        internal Expression Rebuild(IReadOnlyList<Expression> operands)
        {
            ArgumentNullException.ThrowIfNull(operands);
            List<ChainEntry> entries = _entries ?? throw new InvalidOperationException("The chain was extracted without its provenance.");
            if (operands.Count != Terms.Length) throw new ArgumentException($"Expected {Terms.Length} operands, got {operands.Count}.", nameof(operands));

            bool unchanged = true;
            for (int i = 0; i < operands.Count && unchanged; i++) unchanged = ReferenceEquals(operands[i], Terms[i].Operand);
            if (unchanged) return Source;

            // Reverse pre-order: a node is met after its whole right subtree, then its whole left subtree, so the left result
            // is on top of the stack, the right one below it.
            var values = new Stack<Expression>();
            int operand = operands.Count;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                ChainEntry entry = entries[i];
                if (!entry.Crossed)
                {
                    values.Push(operands[--operand]);
                }
                else if (entry.Node is UnaryExpression unary)
                {
                    values.Push(CopyUnaryExpression(unary, values.Pop()));
                }
                else
                {
                    Expression left = values.Pop();
                    Expression right = values.Pop();
                    values.Push(CopyBinaryExpression((BinaryExpression)entry.Node, left, right));
                }
            }

            return values.Pop();
        }
    }

    /// <summary>
    /// Rebuilds the chain rooted at <paramref name="source"/> with new operands: the very operator nodes the flatten rules of
    /// this transformer cross, with their node types, methods and result types, and the same grouping (parentheses) as the
    /// source, so that a rule which transforms every operand can keep the original association instead of rebuilding it from
    /// the signs alone (which <c>(a / b) * c</c> and <c>a / (b / c)</c> share).
    /// </summary>
    /// <param name="source">The root of a chain, as received by a flatten rule.</param>
    /// <param name="operands">One operand per term, in <see cref="FlattenedTerm.SourceIndex"/> order.</param>
    /// <returns><paramref name="source"/> itself when every operand is the original one; otherwise the rebuilt chain.</returns>
    /// <remarks>
    /// The chain is extracted again from <paramref name="source"/> (linear, counted as structural work). When no expression
    /// operation runs on the thread, this call starts one with <see cref="ResourceLimits"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="operands"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> is not the root of a chain of this transformer's flatten rules, or the operand count differs
    /// from the term count.
    /// </exception>
    /// <exception cref="InvalidOperationException">An operand's type does not fit its operator.</exception>
    protected Expression RebuildFlattenedChain(Expression source, IReadOnlyList<Expression> operands)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operands);
        if (!ExpressionResourceBudget.IsActive || (!JoinsRunningOperation && !ReferenceEquals(ExpressionResourceBudget.Owner, this)))
        {
            return ExpressionResourceBudget.Run(
                ResourceLimits,
                this,
                source,
                null,
                (Transformer: this, Source: source, Operands: operands),
                static state => state.Transformer.RebuildFlattenedChainCore(state.Source, state.Operands));
        }

        return RebuildFlattenedChainCore(source, operands);
    }

    /// <summary>The body of <see cref="RebuildFlattenedChain"/>, inside an operation.</summary>
    /// <param name="source">The root of a chain.</param>
    /// <param name="operands">The new operands.</param>
    /// <returns>The rebuilt chain.</returns>
    private Expression RebuildFlattenedChainCore(Expression source, IReadOnlyList<Expression> operands)
    {
        if (FlattenGroupOf(source, out FlattenOperator? root) is not FlattenGroup group)
        {
            throw new ArgumentException("The expression is not the root of a chain of this transformer's flatten rules.", nameof(source));
        }

        return FlattenedChain.Extract(this, source, root!, group, keepProvenance: true).Rebuild(operands);
    }

    /// <summary>
    /// The child a node of a declined chain is preparing: the positional link that keeps the historical fallback of a declined
    /// chain from extracting its sub-chains again. Data only.
    /// </summary>
    /// <param name="Transformer">The transformer whose flatten rules declined the chain.</param>
    /// <param name="Node">The child being prepared, itself crossed by the chain.</param>
    /// <param name="Group">The group of the chain.</param>
    private readonly record struct PendingChainChild(ExpressionTransformer? Transformer, Expression? Node, FlattenGroup? Group);

    /// <summary>
    /// Set by <see cref="PrepareChainChild"/> while a node of a declined chain prepares a child the chain also crosses, and
    /// consumed by the first <see cref="TransformCore(Expression)"/> call of the same transformer on that very child: this call,
    /// and only this one, continues the declined chain (see <see cref="TransformDeclinedChainNode"/>). The link is a position
    /// in the traversal, not a property of the node: another occurrence of the same node object reached by another path (under
    /// a call, for example) finds no link and is offered to the flatten rules as a chain of its own. Per thread, since one
    /// transformer instance may serve several threads; restored in <c>finally</c>.
    /// </summary>
    [ThreadStatic]
    private static PendingChainChild _pendingChainChild;

    /// <summary>
    /// <see cref="TransformCore(Expression)"/> for a transformer that declares flatten rules. When <paramref name="e"/> is the
    /// root of a chain (a crossed node of a declared operator that does not continue a declined chain), the chain is extracted
    /// from the raw tree and given to the rules of its group, in declaration order; the first non-<see langword="null"/>
    /// result is the result of the node. Otherwise, and when every rule declines, the node is transformed exactly as without
    /// flatten rules.
    /// </summary>
    /// <param name="e">The node to transform.</param>
    /// <param name="trace">The tracing state of the operation, or <see langword="null"/>.</param>
    /// <param name="prepared">The node rebuilt by the traversal (for the trace), or <paramref name="e"/> when a flatten rule applied.</param>
    /// <param name="boundary">Whether the node was a transformation boundary.</param>
    /// <returns>The result of the node.</returns>
    private Expression TransformWithFlatten(Expression e, ExpressionTraceSession? trace, out Expression prepared, out bool boundary)
    {
        // A node reached from a rule admitted outside the effect gate gets its own admission, as in TransformPrepared.
        if (_ruleRunsOutsideGate)
        {
            _ruleRunsOutsideGate = false;
            try
            {
                return TransformWithFlatten(e, trace, out prepared, out boundary);
            }
            finally
            {
                _ruleRunsOutsideGate = true;
            }
        }

        // The node continues a declined chain: its flatten rules already declined it as part of that chain.
        PendingChainChild pending = _pendingChainChild;
        if (pending.Node is not null && ReferenceEquals(pending.Node, e) && ReferenceEquals(pending.Transformer, this))
        {
            _pendingChainChild = default;
            return TransformDeclinedChainNode(e, pending.Group!, trace, out prepared, out boundary);
        }

        // Type tests first: the node type of an extension node is never read (ETC R4). A node that belongs to no group (an
        // OperandGroup operator whose operand identifies none, for example) roots no chain.
        if (FlattenGroupOf(e, out FlattenOperator? root) is not FlattenGroup group)
        {
            return TransformHistorically(e, trace, out prepared, out boundary);
        }

        // The admission of the chain, on the raw root: by default the effect gate (inert operands make the whole chain inert);
        // a transformer of this assembly may decide by itself which chains its rules treat (see FlattenAdmission). Flatten
        // rules are never audited, so a refusal leaves the node to the historical path (where audited signature rules may
        // still run outside the gate); the chains below the node are offered when the traversal reaches them.
        ExpressionRuleRejection refusal = FlattenAdmission(e);
        if (refusal != ExpressionRuleRejection.None)
        {
            if (trace is { Detailed: true })
            {
                foreach (TransformRule rule in group.Rules) trace.RuleRejected(rule.Info, e, refusal);
            }

            return TransformHistorically(e, trace, out prepared, out boundary);
        }

        FlattenedChain chain = FlattenedChain.Extract(this, e, root!, group, keepProvenance: false);
        if (trace is { Detailed: true }) trace.ChainFlattened(e);

        // The node is processed here: one transformation visit, before any rule runs (the historical fallback counts its own).
        ExpressionResourceBudget.ConsumeTransformVisit();
        TransformRule[] rules = group.Rules;
        for (int i = 0; i < rules.Length; i++)
        {
            TransformRule rule = rules[i];
            if (trace is { Detailed: true }) trace.RuleCandidate(rule.Info, e);
            trace?.BeginRule(rule.Info);

            // Every rule receives its own array: a declining rule that reorders or overwrites it never affects the next one.
            // The last rule can receive the engine's array itself, which nothing reads afterwards.
            FlattenedTerm[] terms = i == rules.Length - 1 ? chain.Terms : (FlattenedTerm[])chain.Terms.Clone();
            Expression? result = InvokeFlattenRule(rule, e, terms);
            if (result is not null)
            {
                trace?.RuleApplied(rule.Info, e, result);
                prepared = e;
                boundary = false;
                return result;
            }

            trace?.RuleDeclined(rule.Info, e, ExpressionRuleRejection.Declined);
        }

        return TransformDeclinedChainNode(e, group, trace, out prepared, out boundary);
    }

    /// <summary>
    /// <see cref="TransformWithFlatten"/> outside a tracing scope: the single-argument entry of the flatten path, so that
    /// <see cref="TransformCore(Expression)"/> holds none of the call's out locals. Inlining is left to the JIT (no measured
    /// difference either way).
    /// </summary>
    /// <param name="e">The node to transform.</param>
    /// <returns>The result of the node.</returns>
    private Expression TransformWithFlattenUntraced(Expression e) => TransformWithFlatten(e, null, out _, out _);

    /// <summary>The historical part of <see cref="TransformCore(Expression)"/>: preparation, signature rules and finalization.</summary>
    /// <param name="e">The node to transform.</param>
    /// <param name="trace">The tracing state of the operation, or <see langword="null"/>.</param>
    /// <param name="prepared">The node rebuilt by the traversal.</param>
    /// <param name="boundary">Whether the node was a transformation boundary.</param>
    /// <returns>The result of the node.</returns>
    private Expression TransformHistorically(Expression e, ExpressionTraceSession? trace, out Expression prepared, out bool boundary)
    {
        TransformContext context = PrepareTransform(e);
        prepared = context.Expression;
        boundary = context.IsBoundary;
        return TransformPrepared(context, trace);
    }

    /// <summary>
    /// Processes a crossed node of a chain whose flatten rules all declined exactly as without flatten rules: its children go
    /// through <see cref="PrepareExpression"/> in order, the node is rebuilt as <see cref="PrepareBinary"/> and
    /// <see cref="PrepareUnary"/> rebuild it, then the signature rules and <see cref="FinalizeExpression"/> run. The only
    /// difference is the link left for each child the chain also crosses (<see cref="PrepareChainChild"/>), so that the chain
    /// is never extracted again from one of its own nodes.
    /// </summary>
    /// <param name="e">A crossed node of the declined chain: a binary operator, or a unary one with an operand.</param>
    /// <param name="group">The group of the chain.</param>
    /// <param name="trace">The tracing state of the operation, or <see langword="null"/>.</param>
    /// <param name="prepared">The node rebuilt by the traversal.</param>
    /// <param name="boundary">Always <see langword="false"/>: a crossed node is never a transformation boundary.</param>
    /// <returns>The result of the node.</returns>
    private Expression TransformDeclinedChainNode(Expression e, FlattenGroup group, ExpressionTraceSession? trace, out Expression prepared, out bool boundary)
    {
        TransformContext context;
        if (e is BinaryExpression binary)
        {
            Expression left = PrepareChainChild(binary.Left, group);
            Expression right = PrepareChainChild(binary.Right, group);
            context = new TransformContext(CopyBinaryExpression(binary, left, right));
        }
        else
        {
            // The operand of a crossed OperandGroup operator decided its group: the chain crosses it too.
            UnaryExpression unary = (UnaryExpression)e;
            bool known = DescribeFlattenOperator(unary)!.Group is null;
            context = new TransformContext(RebuildUnaryExpression(unary, PrepareChainChild(unary.Operand, group, known)));
        }

        prepared = context.Expression;
        boundary = false;
        return TransformPrepared(context, trace);
    }

    /// <summary>
    /// <see cref="PrepareExpression"/> for a child of a declined chain node. When the chain also crosses the child, the link
    /// <see cref="_pendingChainChild"/> is set for the duration of the call and restored afterwards, even on failure.
    /// </summary>
    /// <param name="child">The raw child.</param>
    /// <param name="group">The group of the chain.</param>
    /// <param name="known">Whether the chain is already known to cross <paramref name="child"/>.</param>
    /// <returns>What <see cref="PrepareExpression"/> returns for <paramref name="child"/>.</returns>
    private Expression PrepareChainChild(Expression child, FlattenGroup group, bool known = false)
    {
        if (!known && CrossingOperator(child, group) is null) return PrepareExpression(child);

        PendingChainChild previous = _pendingChainChild;
        _pendingChainChild = new PendingChainChild(this, child, group);
        try
        {
            return PrepareExpression(child);
        }
        finally
        {
            _pendingChainChild = previous;
        }
    }

    /// <summary>
    /// Invokes a flatten rule with <see cref="MethodBase.Invoke(object, object[])"/>'s exception contract, like the signature
    /// rules (an exception of the rule body is wrapped in a new <see cref="TargetInvocationException"/>; a resource failure
    /// propagates unwrapped).
    /// </summary>
    /// <param name="rule">The flatten rule.</param>
    /// <param name="source">The root of the chain, for a rule that declares it.</param>
    /// <param name="terms">The rule's own copy of the terms.</param>
    /// <returns>The rule's result; <see langword="null"/> when it declines.</returns>
    private Expression? InvokeFlattenRule(TransformRule rule, Expression source, FlattenedTerm[] terms)
    {
        bool withSource = rule.Parameters.Length == 2;
        if (rule.FastInvoker is MethodInvoker invoker)
        {
            try
            {
                return (Expression?)(withSource ? invoker.Invoke(this, source, terms) : invoker.Invoke(this, terms));
            }
            catch (Exception ex) when (!IsResourceFailure(ex))
            {
                // See InvokeSingleRule: wraps exactly like MethodBase.Invoke.
                throw new TargetInvocationException(ex);
            }
        }

        return (Expression?)InvokeRuleReflectively(rule, withSource ? [source, terms] : [terms]);
    }
}
