using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// Applies to one node the signature rules (<see cref="ExpressionSignatureAttribute"/>) that the group of a chain covers, through
/// the dispatch of the transformer that handed it to a <see cref="FlattenAttribute"/> rule: the candidates of the node's type in
/// the transformer's plan, in their declaration order, with the same signature matching, parameter checks, admission, budgets,
/// exception contract and tracing as the node-by-node traversal.
/// </summary>
/// <param name="node">
/// A <em>prepared</em> node of an operator the chain's group declares (its direct or inverse operation, or one of its unary
/// operators): a framework <see cref="BinaryExpression"/> or <see cref="UnaryExpression"/> whose operands the rule has already
/// transformed as it chose. The dispatcher prepares nothing: it neither transforms the operands again, nor extracts a chain from
/// the node, nor calls a flatten rule.
/// </param>
/// <param name="result">
/// The result of the first rule that produced one. A signature rule of the single-node or <c>Expression[]</c> shape may produce
/// <see langword="null"/>, as it does in the traversal; <see cref="ExpressionTransformer"/> keeps that result as is.
/// </param>
/// <returns>
/// <see langword="true"/> when a rule produced <paramref name="result"/>; <see langword="false"/> when no candidate applies,
/// every candidate declines, or the transformer's effect gate refuses the node. In that case the node is unchanged and is not
/// finalized: the flatten rule decides what to do with it.
/// </returns>
/// <remarks>
/// <para>
/// Each call counts one transformation visit and checks the execution stack, like the treatment of a node by the traversal; inside
/// a tracing scope it is a treatment of its own, requested by the running flatten rule. The rules a dispatched node runs may call
/// <c>TransformCore</c> on new nodes: those calls are ordinary transformations, which may root chains of their own.
/// </para>
/// <para>
/// A dispatcher is bound to one transformer instance and one group. It may be kept and called later: outside a running operation it
/// starts one with the limits of its transformer, as <c>RebuildFlattenedChain</c> does.
/// </para>
/// </remarks>
/// <exception cref="System.ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
/// <exception cref="System.ArgumentException">
/// <paramref name="node"/> is not a framework binary or unary node of an operator the group declares.
/// </exception>
public delegate bool FlattenRuleDispatcher(Expression node, out Expression? result);
