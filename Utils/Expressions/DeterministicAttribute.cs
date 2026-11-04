namespace Utils.Expressions;

/// <summary>
/// Declares that a method is deterministic and free of observable side effects (S5 P18 Architecture A). The declaration is
/// read by <see cref="ExpressionBehaviorAnalysis"/>; it is a promise made by the author of the method, never a verified
/// property of its body.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contract.</b> A method marked <c>[Deterministic]</c> promises that, within one process:
/// </para>
/// <list type="bullet">
/// <item><description>
/// its outcome depends only on its arguments (the receiver included for an instance method) and on the state of the objects
/// they reference: called twice with equal arguments, on objects in the same state, it returns equal results, or throws
/// exceptions of the same type both times;
/// </description></item>
/// <item><description>
/// it reads no global or ambient mutable state: no mutable static field, clock, random generator, environment, culture,
/// thread identity, file, network or other input;
/// </description></item>
/// <item><description>
/// it has no observable side effect: it writes no static field, no field of an argument or of an object reachable from one,
/// performs no input or output, raises no event and calls no code that does.
/// </description></item>
/// </list>
/// <para>
/// <b>Exceptions are outside the promise.</b> The declaration says nothing about whether the method throws: the analysis
/// always reports a declared call as <see cref="ExpressionBehaviors.MayThrowUserCode"/>. Allocation is not forbidden either:
/// a method returning a reference type is reported as <see cref="ExpressionBehaviors.Allocates"/>.
/// </para>
/// <para>
/// <b>What the analysis does with it.</b> A declared call is reported as deterministic and free of side effects, with the
/// <see cref="ExpressionBehaviors.ReliesOnDeclaration"/> flag, so a consumer can refuse to rely on declarations. The
/// declaration is honored only where it is bound to the code that runs: on a static method, a non-virtual instance method, a
/// sealed override, or a method of a sealed or value type, that is neither generic over an open type parameter nor takes or
/// returns a value by reference or by pointer nor takes a delegate or an expression tree (code the declaration cannot speak
/// for). It is read on the exact method, never inherited from an overridden or interface method, and recognized by its type
/// only: an attribute of another library that happens to have the same name is ignored, as is
/// <see cref="System.Diagnostics.Contracts.PureAttribute"/>. Since an instance method or a reference-type argument may read
/// the state of the object it references, such a call is also reported as <see cref="ExpressionBehaviors.ReadsMutableState"/>.
/// </para>
/// <para>
/// <b>Status.</b> No simplification policy consumes the declaration yet: <c>ExpressionSimplifier</c> keeps treating every
/// user method as observable. A wrong declaration can therefore not change a simplification result today; it will be able to
/// once a policy that trusts declarations is enabled explicitly. Annotate a property getter or an operator with the
/// attribute on its accessor or operator method (<c>public double X { [Deterministic] get =&gt; …; }</c>).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DeterministicAttribute : Attribute
{
}
