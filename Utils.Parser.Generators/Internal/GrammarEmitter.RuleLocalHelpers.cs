using System.Text;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{

    /// <summary>
    /// Emits explicit rule-local helper methods on the generated execution context.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    private static void EmitRuleLocalHelpers(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>Gets a passive rule-local value from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying the invocation frame to inspect.</param>");
        sb.AppendLine("    /// <param name=\"name\">Rule-local metadata name.</param>");
        sb.AppendLine("    /// <returns>The stored local value, or <c>null</c> when no invocation frame or local value is present.</returns>");
        sb.AppendLine("    /// <remarks>Declared locals are allocated separately before generated <c>@init</c> execution.</remarks>");
        sb.AppendLine("    private static object? GetRuleLocal(ParserRuleLifecycleContext context, string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(name);");
        sb.AppendLine();
        sb.AppendLine("        return TryGetRuleLocal(context, name, out object? value) ? value : null;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Attempts to get a passive rule-local value from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying the invocation frame to inspect.</param>");
        sb.AppendLine("    /// <param name=\"name\">Rule-local metadata name.</param>");
        sb.AppendLine("    /// <param name=\"value\">Receives the stored local value when one has been explicitly set.</param>");
        sb.AppendLine("    /// <returns><c>true</c> when the invocation frame contains the named local; otherwise, <c>false</c>.</returns>");
        sb.AppendLine("    /// <remarks>This helper observes only <c>context.InvocationFrame</c>; declared-local allocation is handled before generated <c>@init</c>.</remarks>");
        sb.AppendLine("    private static bool TryGetRuleLocal(ParserRuleLifecycleContext context, string name, out object? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(name);");
        sb.AppendLine();
        sb.AppendLine("        if (context.InvocationFrame is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return context.InvocationFrame.TryGetLocal(name, out value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets a required typed rule-local value from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    private static T GetRequiredRuleLocal<T>(ParserRuleLifecycleContext context, string localName)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(localName);");
        sb.AppendLine("        if (context.InvocationFrame is null || !context.InvocationFrame.TryGetLocal(localName, out object? value))");
        sb.AppendLine("        {");
        sb.AppendLine(@"            throw new global::Utils.Parser.Runtime.ParserAttributeAccessException(""$"" + localName, localName, ""Rule local '"" + localName + ""' is not available on the current rule invocation."");");
        sb.AppendLine("        }");
        sb.AppendLine("        return CastRequiredRuleAttribute<T>(\"$\" + localName, \"local\", localName, value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Ensures a declared rule-local slot exists on the active invocation frame before a typed write.</summary>");
        sb.AppendLine("    private static void EnsureRequiredRuleLocalPresent(ParserRuleInvocationFrame frame, string localName)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (frame.TryGetLocal(localName, out _))");
        sb.AppendLine("        {");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (frame.Descriptor is not null && global::System.Linq.Enumerable.Any(frame.Descriptor.Locals, descriptor => string.Equals(descriptor.Name, localName, global::System.StringComparison.Ordinal)))");
        sb.AppendLine("        {");
        sb.AppendLine("            frame.SetLocal(localName, null);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine(@"        throw new global::Utils.Parser.Runtime.ParserAttributeAccessException(""$"" + localName, localName, ""Rule local '"" + localName + ""' is not available on the current rule invocation."");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Stores a required typed rule-local value on the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    private static void SetRequiredRuleLocal<T>(ParserRuleLifecycleContext context, string localName, T value)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(localName);");
        sb.AppendLine("        if (context.InvocationFrame is null)");
        sb.AppendLine("        {");
        sb.AppendLine(@"            throw new global::Utils.Parser.Runtime.ParserAttributeAccessException(""$"" + localName, localName, ""Rule local '"" + localName + ""' is not available on the current rule invocation."");");
        sb.AppendLine("        }");
        sb.AppendLine("        EnsureRequiredRuleLocalPresent(context.InvocationFrame, localName);");
        sb.AppendLine("        context.InvocationFrame.SetLocal(localName, value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Stores a passive rule-local value on the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying the invocation frame to update.</param>");
        sb.AppendLine("    /// <param name=\"name\">Rule-local metadata name.</param>");
        sb.AppendLine("    /// <param name=\"value\">Value to store in the invocation-frame locals dictionary.</param>");
        sb.AppendLine("    /// <remarks>If no invocation frame is available, this helper performs no work and does not allocate a frame.</remarks>");
        sb.AppendLine("    private static void SetRuleLocal(ParserRuleLifecycleContext context, string name, object? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(name);");
        sb.AppendLine();
        sb.AppendLine("        context.InvocationFrame?.SetLocal(name, value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets passive rule-local declaration descriptors from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying descriptor metadata.</param>");
        sb.AppendLine("    /// <returns>Rule-local declaration descriptors, or an empty list when no descriptor metadata is available.</returns>");
        sb.AppendLine("    /// <remarks>The descriptors contain raw declarations only; generated code does not infer C# types or create typed locals.</remarks>");
        sb.AppendLine("    private static IReadOnlyList<ParserRuleLocalDescriptor> GetRuleLocalDescriptors(ParserRuleLifecycleContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine();
        sb.AppendLine("        return context.InvocationFrame?.Descriptor?.Locals ?? global::System.Array.Empty<ParserRuleLocalDescriptor>();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Allocates missing declared rule locals as untyped null entries on the active invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying the invocation frame and passive local descriptors.</param>");
        sb.AppendLine("    /// <remarks>Existing values are preserved, and no target-language type or default value is inferred.</remarks>");
        sb.AppendLine("    private static void AllocateDeclaredRuleLocals(ParserRuleLifecycleContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine();
        sb.AppendLine("        ParserRuleInvocationFrame? frame = context.InvocationFrame;");
        sb.AppendLine("        if (frame?.Descriptor is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        foreach (ParserRuleLocalDescriptor descriptor in frame.Descriptor.Locals)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!string.IsNullOrEmpty(descriptor.Name) && !frame.TryGetLocal(descriptor.Name, out _))");
        sb.AppendLine("            {");
        sb.AppendLine("                frame.SetLocal(descriptor.Name, null);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
