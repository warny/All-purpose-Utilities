using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Utils.Expressions;

/// <summary>
/// Read-only walk of the bindings and initializers of the engine-internal <see cref="ExpressionVisitor"/>s (ETC R3): the
/// free-variable walk of the substitution and the reference finder of the inlining analysis. A binding or an initializer is
/// not an expression node, and nested member bindings nest without any expression node in between, so the framework visitor
/// would recurse through them without stack check or budget, building a delegate to a virtual method at every level (native
/// runtime code that may need more stack than the guard keeps). This walk counts one structural visit per binding and per
/// initializer (stack check included), visits every expression through the visitor's own
/// <see cref="ExpressionVisitor.Visit(Expression?)"/>, and never rebuilds anything.
/// </summary>
/// <remarks>
/// The walk is not iterative: nested member bindings still recurse (<see cref="VisitBinding"/> calls
/// <see cref="VisitBindings"/> for a <see cref="MemberMemberBinding"/>), one plain managed call per level, without any delegate.
/// Every level enters <see cref="VisitBinding"/>, whose <see cref="ExpressionResourceBudget.ConsumeStructuralVisit"/> checks the
/// stack before descending, so a hostile nesting ends with a controlled refusal instead of a stack overflow.
/// </remarks>
internal static class ReadOnlyBindingWalk
{
    /// <summary>Visits bindings in declaration order.</summary>
    /// <param name="visitor">The read-only visitor.</param>
    /// <param name="bindings">The bindings.</param>
    public static void VisitBindings(ExpressionVisitor visitor, ReadOnlyCollection<MemberBinding> bindings)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            VisitBinding(visitor, bindings[i]);
        }
    }

    /// <summary>Visits one binding: its value, its nested bindings, or its initializers (an unknown binding class has no known child).</summary>
    /// <param name="visitor">The read-only visitor.</param>
    /// <param name="binding">The binding.</param>
    public static void VisitBinding(ExpressionVisitor visitor, MemberBinding binding)
    {
        ExpressionResourceBudget.ConsumeStructuralVisit();
        switch (binding)
        {
            case MemberAssignment assignment:
                visitor.Visit(assignment.Expression);
                break;
            case MemberMemberBinding memberBinding:
                VisitBindings(visitor, memberBinding.Bindings);
                break;
            case MemberListBinding listBinding:
                VisitInitializers(visitor, listBinding.Initializers);
                break;
        }
    }

    /// <summary>Visits initializers in order.</summary>
    /// <param name="visitor">The read-only visitor.</param>
    /// <param name="initializers">The initializers.</param>
    public static void VisitInitializers(ExpressionVisitor visitor, ReadOnlyCollection<ElementInit> initializers)
    {
        for (int i = 0; i < initializers.Count; i++)
        {
            VisitInitializer(visitor, initializers[i]);
        }
    }

    /// <summary>Visits the arguments of one initializer, in order.</summary>
    /// <param name="visitor">The read-only visitor.</param>
    /// <param name="initializer">The initializer.</param>
    public static void VisitInitializer(ExpressionVisitor visitor, ElementInit initializer)
    {
        ExpressionResourceBudget.ConsumeStructuralVisit();
        for (int i = 0; i < initializer.Arguments.Count; i++)
        {
            visitor.Visit(initializer.Arguments[i]);
        }
    }
}
