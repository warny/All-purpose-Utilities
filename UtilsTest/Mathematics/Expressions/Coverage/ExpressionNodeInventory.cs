using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>The role a public property of an expression-tree class plays for traversal, rebuilding, comparison and scoping.</summary>
internal enum NodeFieldRole
{
    /// <summary>A mandatory <see cref="Expression"/> child.</summary>
    Child,

    /// <summary>An <see cref="Expression"/> child that may be <see langword="null"/>.</summary>
    OptionalChild,

    /// <summary>An ordered collection of <see cref="Expression"/> children.</summary>
    Children,

    /// <summary>A non-<see cref="Expression"/> structural object or collection (<see cref="CatchBlock"/>, <see cref="SwitchCase"/>, <see cref="MemberBinding"/>, <see cref="ElementInit"/>) that owns children.</summary>
    Structure,

    /// <summary>A declaration introducing a lexical binding (lambda parameters, block variables, catch variable).</summary>
    Declaration,

    /// <summary>A reference to variables that must stay <see cref="ParameterExpression"/> nodes (runtime variables).</summary>
    VariableReference,

    /// <summary>A <see cref="LabelTarget"/> whose identity, not its name, links jumps to their destination.</summary>
    LabelIdentity,

    /// <summary>Semantic metadata that changes what the node does (methods, constructors, members, types, binders, flags).</summary>
    Semantic,

    /// <summary>Debug-only metadata (names, documents, line information).</summary>
    Debug,

    /// <summary>A value computed from other properties; never an independent input of a factory.</summary>
    Derived,
}

/// <summary>
/// Programmatically checked inventory of the public expression-tree object model: every public class and every
/// public property it declares, with the <see cref="NodeFieldRole"/> it plays, plus the public class produced for
/// every <see cref="ExpressionType"/>.
/// </summary>
internal static class ExpressionNodeInventory
{
    /// <summary>The structural classes that are not <see cref="Expression"/> nodes but own children or identities.</summary>
    public static readonly Type[] StructuralTypes =
    [
        typeof(CatchBlock), typeof(SwitchCase), typeof(LabelTarget), typeof(MemberBinding), typeof(MemberAssignment),
        typeof(MemberMemberBinding), typeof(MemberListBinding), typeof(ElementInit), typeof(SymbolDocumentInfo),
    ];

    /// <summary>Every public property declared by each inventoried class, with its role.</summary>
    public static readonly IReadOnlyDictionary<Type, IReadOnlyDictionary<string, NodeFieldRole>> Fields = new Dictionary<Type, IReadOnlyDictionary<string, NodeFieldRole>>
    {
        [typeof(Expression)] = Roles(("NodeType", NodeFieldRole.Derived), ("Type", NodeFieldRole.Derived), ("CanReduce", NodeFieldRole.Derived)),
        [typeof(BinaryExpression)] = Roles(
            ("Left", NodeFieldRole.Child), ("Right", NodeFieldRole.Child), ("Method", NodeFieldRole.Semantic),
            ("Conversion", NodeFieldRole.OptionalChild), ("IsLifted", NodeFieldRole.Derived), ("IsLiftedToNull", NodeFieldRole.Semantic),
            ("CanReduce", NodeFieldRole.Derived)),
        [typeof(BlockExpression)] = Roles(
            ("Expressions", NodeFieldRole.Children), ("Variables", NodeFieldRole.Declaration), ("Result", NodeFieldRole.Derived),
            ("NodeType", NodeFieldRole.Derived), ("Type", NodeFieldRole.Semantic)),
        [typeof(CatchBlock)] = Roles(
            ("Variable", NodeFieldRole.Declaration), ("Test", NodeFieldRole.Semantic), ("Body", NodeFieldRole.Child), ("Filter", NodeFieldRole.OptionalChild)),
        [typeof(ConditionalExpression)] = Roles(
            ("NodeType", NodeFieldRole.Derived), ("Type", NodeFieldRole.Semantic), ("Test", NodeFieldRole.Child),
            ("IfTrue", NodeFieldRole.Child), ("IfFalse", NodeFieldRole.Child)),
        [typeof(ConstantExpression)] = Roles(("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Derived), ("Value", NodeFieldRole.Semantic)),
        [typeof(DebugInfoExpression)] = Roles(
            ("Type", NodeFieldRole.Derived), ("NodeType", NodeFieldRole.Derived), ("StartLine", NodeFieldRole.Debug), ("StartColumn", NodeFieldRole.Debug),
            ("EndLine", NodeFieldRole.Debug), ("EndColumn", NodeFieldRole.Debug), ("Document", NodeFieldRole.Debug), ("IsClear", NodeFieldRole.Derived)),
        [typeof(DefaultExpression)] = Roles(("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Derived)),
        [typeof(DynamicExpression)] = Roles(
            ("CanReduce", NodeFieldRole.Derived), ("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Derived),
            ("Binder", NodeFieldRole.Semantic), ("DelegateType", NodeFieldRole.Semantic), ("Arguments", NodeFieldRole.Children)),
        [typeof(ElementInit)] = Roles(("AddMethod", NodeFieldRole.Semantic), ("Arguments", NodeFieldRole.Children), ("ArgumentCount", NodeFieldRole.Derived)),
        [typeof(Expression<>)] = Roles(),
        [typeof(GotoExpression)] = Roles(
            ("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Derived), ("Value", NodeFieldRole.OptionalChild),
            ("Target", NodeFieldRole.LabelIdentity), ("Kind", NodeFieldRole.Semantic)),
        [typeof(IndexExpression)] = Roles(
            ("NodeType", NodeFieldRole.Derived), ("Type", NodeFieldRole.Derived), ("Object", NodeFieldRole.Child),
            ("Indexer", NodeFieldRole.Semantic), ("Arguments", NodeFieldRole.Children), ("ArgumentCount", NodeFieldRole.Derived)),
        [typeof(InvocationExpression)] = Roles(
            ("Type", NodeFieldRole.Derived), ("NodeType", NodeFieldRole.Derived), ("Expression", NodeFieldRole.Child),
            ("Arguments", NodeFieldRole.Children), ("ArgumentCount", NodeFieldRole.Derived)),
        [typeof(LabelExpression)] = Roles(
            ("Type", NodeFieldRole.Derived), ("NodeType", NodeFieldRole.Derived), ("Target", NodeFieldRole.LabelIdentity), ("DefaultValue", NodeFieldRole.OptionalChild)),
        [typeof(LabelTarget)] = Roles(("Name", NodeFieldRole.Debug), ("Type", NodeFieldRole.Semantic)),
        [typeof(LambdaExpression)] = Roles(
            ("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Derived), ("Parameters", NodeFieldRole.Declaration),
            ("Name", NodeFieldRole.Debug), ("Body", NodeFieldRole.Child), ("ReturnType", NodeFieldRole.Derived), ("TailCall", NodeFieldRole.Semantic)),
        [typeof(ListInitExpression)] = Roles(
            ("NodeType", NodeFieldRole.Derived), ("Type", NodeFieldRole.Derived), ("CanReduce", NodeFieldRole.Derived),
            ("NewExpression", NodeFieldRole.Child), ("Initializers", NodeFieldRole.Structure)),
        [typeof(LoopExpression)] = Roles(
            ("Type", NodeFieldRole.Derived), ("NodeType", NodeFieldRole.Derived), ("Body", NodeFieldRole.Child),
            ("BreakLabel", NodeFieldRole.LabelIdentity), ("ContinueLabel", NodeFieldRole.LabelIdentity)),
        [typeof(MemberAssignment)] = Roles(("Expression", NodeFieldRole.Child)),
        [typeof(MemberBinding)] = Roles(("BindingType", NodeFieldRole.Derived), ("Member", NodeFieldRole.Semantic)),
        [typeof(MemberExpression)] = Roles(("Member", NodeFieldRole.Semantic), ("Expression", NodeFieldRole.OptionalChild), ("NodeType", NodeFieldRole.Derived)),
        [typeof(MemberInitExpression)] = Roles(
            ("Type", NodeFieldRole.Derived), ("CanReduce", NodeFieldRole.Derived), ("NodeType", NodeFieldRole.Derived),
            ("NewExpression", NodeFieldRole.Child), ("Bindings", NodeFieldRole.Structure)),
        [typeof(MemberListBinding)] = Roles(("Initializers", NodeFieldRole.Structure)),
        [typeof(MemberMemberBinding)] = Roles(("Bindings", NodeFieldRole.Structure)),
        [typeof(MethodCallExpression)] = Roles(
            ("NodeType", NodeFieldRole.Derived), ("Type", NodeFieldRole.Derived), ("Method", NodeFieldRole.Semantic),
            ("Object", NodeFieldRole.OptionalChild), ("Arguments", NodeFieldRole.Children), ("ArgumentCount", NodeFieldRole.Derived)),
        [typeof(NewArrayExpression)] = Roles(("Type", NodeFieldRole.Semantic), ("Expressions", NodeFieldRole.Children)),
        [typeof(NewExpression)] = Roles(
            ("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Derived), ("Constructor", NodeFieldRole.Semantic),
            ("Arguments", NodeFieldRole.Children), ("ArgumentCount", NodeFieldRole.Derived), ("Members", NodeFieldRole.Semantic)),
        [typeof(ParameterExpression)] = Roles(
            ("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Derived), ("Name", NodeFieldRole.Debug), ("IsByRef", NodeFieldRole.Semantic)),
        [typeof(RuntimeVariablesExpression)] = Roles(("Type", NodeFieldRole.Derived), ("NodeType", NodeFieldRole.Derived), ("Variables", NodeFieldRole.VariableReference)),
        [typeof(SwitchCase)] = Roles(("TestValues", NodeFieldRole.Children), ("Body", NodeFieldRole.Child)),
        [typeof(SwitchExpression)] = Roles(
            ("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Derived), ("SwitchValue", NodeFieldRole.Child),
            ("Cases", NodeFieldRole.Structure), ("DefaultBody", NodeFieldRole.OptionalChild), ("Comparison", NodeFieldRole.Semantic)),
        [typeof(SymbolDocumentInfo)] = Roles(
            ("FileName", NodeFieldRole.Debug), ("Language", NodeFieldRole.Debug), ("LanguageVendor", NodeFieldRole.Debug), ("DocumentType", NodeFieldRole.Debug)),
        [typeof(TryExpression)] = Roles(
            ("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Derived), ("Body", NodeFieldRole.Child),
            ("Handlers", NodeFieldRole.Structure), ("Finally", NodeFieldRole.OptionalChild), ("Fault", NodeFieldRole.OptionalChild)),
        [typeof(TypeBinaryExpression)] = Roles(
            ("Type", NodeFieldRole.Derived), ("NodeType", NodeFieldRole.Semantic), ("Expression", NodeFieldRole.Child), ("TypeOperand", NodeFieldRole.Semantic)),
        [typeof(UnaryExpression)] = Roles(
            ("Type", NodeFieldRole.Semantic), ("NodeType", NodeFieldRole.Semantic), ("Operand", NodeFieldRole.OptionalChild),
            ("Method", NodeFieldRole.Semantic), ("IsLifted", NodeFieldRole.Derived), ("IsLiftedToNull", NodeFieldRole.Derived), ("CanReduce", NodeFieldRole.Derived)),
    };

    /// <summary>
    /// The public class of the root node built for every <see cref="ExpressionType"/>. <see cref="ExpressionType.Extension"/>
    /// maps to <see cref="Expression"/> itself: an extension node is any user-defined subclass.
    /// </summary>
    public static readonly IReadOnlyDictionary<ExpressionType, Type> NodeClasses = BuildNodeClasses();

    /// <summary>Discovers every public expression-tree class of the running framework that the inventory must cover.</summary>
    /// <returns>The exported <see cref="Expression"/>-derived classes of <c>System.Linq.Expressions</c>, plus <see cref="StructuralTypes"/>.</returns>
    public static IEnumerable<Type> DiscoverPublicClasses()
        => typeof(Expression).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == "System.Linq.Expressions" && type.IsClass
                && (typeof(Expression).IsAssignableFrom(type) || StructuralTypes.Contains(type)));

    /// <summary>Gets the public properties a class declares itself (not inherited ones).</summary>
    /// <param name="type">The class to inspect.</param>
    /// <returns>The declared public instance property names.</returns>
    public static IEnumerable<string> DeclaredPublicProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(property => property.Name);

    /// <summary>Gets the closest public class of the expression-tree object model for a runtime node type.</summary>
    /// <param name="runtimeType">The runtime type of a node (often an internal framework subclass).</param>
    /// <returns>The closest public base class declared in <c>System.Linq.Expressions</c>, or <see cref="Expression"/> for a user-defined subclass.</returns>
    public static Type PublicClassOf(Type runtimeType)
    {
        Type type = runtimeType;
        while (!(type.IsPublic && type.Namespace == "System.Linq.Expressions"))
        {
            type = type.BaseType!;
        }

        return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
    }

    /// <summary>Builds a read-only role map.</summary>
    /// <param name="roles">The property roles.</param>
    /// <returns>The role map.</returns>
    private static IReadOnlyDictionary<string, NodeFieldRole> Roles(params (string Name, NodeFieldRole Role)[] roles)
        => roles.ToDictionary(role => role.Name, role => role.Role);

    /// <summary>Builds <see cref="NodeClasses"/>.</summary>
    /// <returns>The map of every <see cref="ExpressionType"/> to its public class.</returns>
    private static Dictionary<ExpressionType, Type> BuildNodeClasses()
    {
        var map = new Dictionary<ExpressionType, Type>();
        void Map(Type type, params ExpressionType[] nodeTypes)
        {
            foreach (ExpressionType nodeType in nodeTypes) map.Add(nodeType, type);
        }

        Map(typeof(BinaryExpression),
            ExpressionType.Add, ExpressionType.AddChecked, ExpressionType.Subtract, ExpressionType.SubtractChecked,
            ExpressionType.Multiply, ExpressionType.MultiplyChecked, ExpressionType.Divide, ExpressionType.Modulo, ExpressionType.Power,
            ExpressionType.And, ExpressionType.Or, ExpressionType.ExclusiveOr, ExpressionType.LeftShift, ExpressionType.RightShift,
            ExpressionType.Equal, ExpressionType.NotEqual, ExpressionType.GreaterThan, ExpressionType.GreaterThanOrEqual,
            ExpressionType.LessThan, ExpressionType.LessThanOrEqual, ExpressionType.AndAlso, ExpressionType.OrElse,
            ExpressionType.Coalesce, ExpressionType.ArrayIndex, ExpressionType.Assign,
            ExpressionType.AddAssign, ExpressionType.AddAssignChecked, ExpressionType.SubtractAssign, ExpressionType.SubtractAssignChecked,
            ExpressionType.MultiplyAssign, ExpressionType.MultiplyAssignChecked, ExpressionType.DivideAssign, ExpressionType.ModuloAssign,
            ExpressionType.PowerAssign, ExpressionType.AndAssign, ExpressionType.OrAssign, ExpressionType.ExclusiveOrAssign,
            ExpressionType.LeftShiftAssign, ExpressionType.RightShiftAssign);
        Map(typeof(UnaryExpression),
            ExpressionType.Negate, ExpressionType.NegateChecked, ExpressionType.UnaryPlus, ExpressionType.Not, ExpressionType.OnesComplement,
            ExpressionType.Increment, ExpressionType.Decrement, ExpressionType.IsTrue, ExpressionType.IsFalse, ExpressionType.Convert,
            ExpressionType.ConvertChecked, ExpressionType.TypeAs, ExpressionType.Unbox, ExpressionType.ArrayLength, ExpressionType.Quote,
            ExpressionType.Throw, ExpressionType.PreIncrementAssign, ExpressionType.PreDecrementAssign, ExpressionType.PostIncrementAssign,
            ExpressionType.PostDecrementAssign);
        Map(typeof(TypeBinaryExpression), ExpressionType.TypeIs, ExpressionType.TypeEqual);
        Map(typeof(NewArrayExpression), ExpressionType.NewArrayInit, ExpressionType.NewArrayBounds);
        Map(typeof(ConstantExpression), ExpressionType.Constant);
        Map(typeof(ParameterExpression), ExpressionType.Parameter);
        Map(typeof(DefaultExpression), ExpressionType.Default);
        Map(typeof(MethodCallExpression), ExpressionType.Call);
        Map(typeof(MemberExpression), ExpressionType.MemberAccess);
        Map(typeof(InvocationExpression), ExpressionType.Invoke);
        Map(typeof(Expression<>), ExpressionType.Lambda);
        Map(typeof(ConditionalExpression), ExpressionType.Conditional);
        Map(typeof(NewExpression), ExpressionType.New);
        Map(typeof(MemberInitExpression), ExpressionType.MemberInit);
        Map(typeof(ListInitExpression), ExpressionType.ListInit);
        Map(typeof(IndexExpression), ExpressionType.Index);
        Map(typeof(BlockExpression), ExpressionType.Block);
        Map(typeof(TryExpression), ExpressionType.Try);
        Map(typeof(SwitchExpression), ExpressionType.Switch);
        Map(typeof(LoopExpression), ExpressionType.Loop);
        Map(typeof(GotoExpression), ExpressionType.Goto);
        Map(typeof(LabelExpression), ExpressionType.Label);
        Map(typeof(RuntimeVariablesExpression), ExpressionType.RuntimeVariables);
        Map(typeof(DebugInfoExpression), ExpressionType.DebugInfo);
        Map(typeof(DynamicExpression), ExpressionType.Dynamic);
        Map(typeof(Expression), ExpressionType.Extension);
        return map;
    }

    /// <summary>Gets the parameter types of every overridable <c>Visit*</c> method of <see cref="ExpressionVisitor"/>.</summary>
    /// <returns>The visited types; generic parameters are reported as their generic type definition.</returns>
    public static IEnumerable<Type> VisitorVisitedTypes()
        => typeof(ExpressionVisitor).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(method => method.IsVirtual && method.Name.StartsWith("Visit", StringComparison.Ordinal) && method.GetParameters().Length == 1)
            .Select(method => method.GetParameters()[0].ParameterType)
            .Select(type => type.IsGenericType ? type.GetGenericTypeDefinition() : type)
            .Distinct();
}
