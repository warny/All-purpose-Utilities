using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Utils.Expressions;

/// <summary>
/// Decides whether a reflection object found in an expression tree (the method of a call, operator or conversion, the
/// member of an access, a constructor or an indexer) was produced by the runtime itself, so that the engine may read its
/// metadata and rely on its equality (S5 A-10). This is the single provenance check of the expression engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why.</b> The framework factories accept any subclass of <see cref="MethodInfo"/>, <see cref="FieldInfo"/>,
/// <see cref="PropertyInfo"/> or <see cref="ConstructorInfo"/>. The members of such a subclass (<c>DeclaringType</c>,
/// <c>Name</c>, <c>Attributes</c>, <c>GetParameters</c>, <c>IsDefined</c>, <c>Equals</c>, <c>GetHashCode</c>…) are user code:
/// they can impersonate <c>Math.Sin</c> or <c>decimal.op_Addition</c>, claim that a field is read-only, answer
/// <see langword="true"/> to any equality, count their calls or throw. Before A-10, the effect gate, the "ordinary arithmetic"
/// checks and the call-rule signatures trusted that metadata, so a user call could be dropped or replaced.
/// </para>
/// <para>
/// <b>How.</b> The concrete class of the object is compared, by reference, with the closed set of classes the runtime uses
/// for the reflection objects of loaded code: its methods (ordinary, generic definitions and instantiations, array methods, and
/// the methods of created dynamic and collectible types), constructors, fields (ordinary and literal), properties, and
/// <see cref="DynamicMethod"/>. <see cref="object.GetType"/> is not virtual, so the test runs no code of the object. The classes
/// are read at type initialization from members of <see cref="object"/>, <see cref="string"/> and <see cref="int"/>, never
/// named.
/// </para>
/// <para>
/// <b>Guarantee.</b> An object accepted here is a runtime reflection object: its metadata describes the member it stands for
/// and its equality and hashing are the runtime's. Acceptance grants no trust in the member's code: a user method obtained
/// through ordinary reflection is accepted, and stays user code for every analysis.
/// </para>
/// <para>
/// <b>Limits.</b> Every other class is refused, conservatively: user subclasses (including subclasses of public framework
/// classes such as <c>MethodBuilder</c>), the builders of <see cref="System.Reflection.Emit"/> and their generic
/// instantiations (which can forward to objects supplied by user code), and the reflection objects of other loaders (for
/// example <c>MetadataLoadContext</c>). A refused object is classified as observable unknown code, never matched by a rule and
/// never inlined. The check covers reflection objects held by nodes; the <see cref="Type"/> of a node is out of its scope (see
/// the A-10 note).
/// </para>
/// </remarks>
internal static class ExpressionReflectionProvenance
{
    /// <summary>The runtime class of methods.</summary>
    private static readonly Type RuntimeMethod = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!.GetType();

    /// <summary>The runtime class of constructors.</summary>
    private static readonly Type RuntimeConstructor = typeof(object).GetConstructor(Type.EmptyTypes)!.GetType();

    /// <summary>The runtime class of ordinary fields.</summary>
    private static readonly Type RuntimeField = typeof(string).GetField(nameof(string.Empty))!.GetType();

    /// <summary>The runtime class of literal (constant) fields.</summary>
    private static readonly Type RuntimeLiteralField = typeof(int).GetField(nameof(int.MaxValue))!.GetType();

    /// <summary>The runtime class of properties.</summary>
    private static readonly Type RuntimeProperty = typeof(string).GetProperty(nameof(string.Length))!.GetType();

    /// <summary>
    /// Whether <paramref name="member"/> is a reflection object of the runtime itself (see the remarks of the class). Runs no
    /// code of the object.
    /// </summary>
    /// <param name="member">The reflection object, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for a runtime reflection object; <see langword="false"/> for any other object or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsRuntime(MemberInfo? member)
    {
        if (member is null) return false;
        Type type = member.GetType();
        return ReferenceEquals(type, RuntimeMethod)
            || ReferenceEquals(type, RuntimeField)
            || ReferenceEquals(type, RuntimeProperty)
            || ReferenceEquals(type, RuntimeConstructor)
            || ReferenceEquals(type, RuntimeLiteralField)
            || ReferenceEquals(type, typeof(DynamicMethod));
    }

    /// <summary>
    /// Whether <paramref name="actual"/> is the runtime member <paramref name="expected"/>: both absent, or
    /// <paramref name="actual"/> is a runtime reflection object equal to it. The equality of an object that is not a runtime one
    /// is never called.
    /// </summary>
    /// <param name="actual">The member found in a node, or <see langword="null"/>.</param>
    /// <param name="expected">The expected runtime member, or <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> when <paramref name="actual"/> is <paramref name="expected"/>.</returns>
    /// <remarks>
    /// The reference test comes first: it is the common case, and the identity of <paramref name="expected"/> (a runtime object)
    /// proves the provenance of <paramref name="actual"/> without any call.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSameMember(MemberInfo? actual, MemberInfo? expected)
    {
        if (ReferenceEquals(actual, expected)) return true;
        return actual is not null && expected is not null && IsRuntime(actual) && actual.Equals(expected);
    }
}
