using System.Text;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{

    /// <summary>
    /// Emits explicit rule-return helper methods on the generated execution context.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    private static void EmitRuleReturnHelpers(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>Gets an untyped rule-return value from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying the invocation frame to inspect.</param>");
        sb.AppendLine("    /// <param name=\"name\">Rule-return metadata name.</param>");
        sb.AppendLine("    /// <returns>The stored return value, or <c>null</c> when no invocation frame or return value is present.</returns>");
        sb.AppendLine("    /// <remarks>Returns are not propagated to caller frames. Generated <c>Parse(...)</c> remains conservative.</remarks>");
        sb.AppendLine("    private static object? GetRuleReturn(ParserRuleLifecycleContext context, string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(name);");
        sb.AppendLine();
        sb.AppendLine("        return TryGetRuleReturn(context, name, out object? value) ? value : null;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Attempts to get an untyped rule-return value from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying the invocation frame to inspect.</param>");
        sb.AppendLine("    /// <param name=\"name\">Rule-return metadata name.</param>");
        sb.AppendLine("    /// <param name=\"value\">Receives the stored return value when one has been explicitly set.</param>");
        sb.AppendLine("    /// <returns><c>true</c> when the invocation frame contains the named return; otherwise, <c>false</c>.</returns>");
        sb.AppendLine("    /// <remarks>Returns are not propagated to caller frames. Return values must be written explicitly via <c>SetRuleReturn</c>.</remarks>");
        sb.AppendLine("    private static bool TryGetRuleReturn(ParserRuleLifecycleContext context, string name, out object? value)");
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
        sb.AppendLine("        return context.InvocationFrame.TryGetReturnValue(name, out value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets a required current-rule return while preserving present-null values.</summary>");
        sb.AppendLine("    private static object? GetRequiredRuleReturn(ParserRuleLifecycleContext context, string returnName)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(returnName);");
        sb.AppendLine("        if (context.InvocationFrame is not null && context.InvocationFrame.TryGetReturnValue(returnName, out object? value)) return value;");
        sb.AppendLine(@"        throw new global::Utils.Parser.Runtime.ParserAttributeAccessException(""$"" + context.RuleName + ""."" + returnName, returnName, ""Return '"" + returnName + ""' is not available on the current rule invocation."");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets a typed required current-rule return while preserving present-null values.</summary>");
        sb.AppendLine("    private static T GetRequiredRuleReturn<T>(ParserRuleLifecycleContext context, string returnName)");
        sb.AppendLine("    {");
        sb.AppendLine("        return (T)GetRequiredRuleReturn(context, returnName)!;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Ensures a declared rule-return slot exists on the active invocation frame before a typed write.</summary>");
        sb.AppendLine("    private static void EnsureRequiredRuleReturnPresent(ParserRuleInvocationFrame frame, string returnName, string ruleName)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (frame.TryGetReturnValue(returnName, out _))");
        sb.AppendLine("        {");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (frame.Descriptor is not null && global::System.Linq.Enumerable.Any(frame.Descriptor.Returns, descriptor => string.Equals(descriptor.Name, returnName, global::System.StringComparison.Ordinal)))");
        sb.AppendLine("        {");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine(@"        throw new global::Utils.Parser.Runtime.ParserAttributeAccessException(""$"" + ruleName + ""."" + returnName, returnName, ""Return '"" + returnName + ""' is not declared on the current rule invocation."");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Stores a typed rule-return value on the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    private static void SetRequiredRuleReturn<T>(ParserRuleLifecycleContext context, string returnName, T value)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(returnName);");
        sb.AppendLine("        if (context.InvocationFrame is null)");
        sb.AppendLine("        {");
        sb.AppendLine(@"            throw new global::Utils.Parser.Runtime.ParserAttributeAccessException(""$"" + context.RuleName + ""."" + returnName, returnName, ""Return '"" + returnName + ""' is not available on the current rule invocation."");");
        sb.AppendLine("        }");
        sb.AppendLine("        EnsureRequiredRuleReturnPresent(context.InvocationFrame, returnName, context.RuleName);");
        sb.AppendLine("        context.InvocationFrame.SetReturnValue(returnName, value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Stores an untyped rule-return value on the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying the invocation frame to update.</param>");
        sb.AppendLine("    /// <param name=\"name\">Rule-return metadata name.</param>");
        sb.AppendLine("    /// <param name=\"value\">Value to store in the invocation-frame returns dictionary.</param>");
        sb.AppendLine("    /// <remarks>If no invocation frame is available, this helper performs no work. Returns are not propagated to caller frames automatically.</remarks>");
        sb.AppendLine("    private static void SetRuleReturn(ParserRuleLifecycleContext context, string name, object? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(name);");
        sb.AppendLine();
        sb.AppendLine("        context.InvocationFrame?.SetReturnValue(name, value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets passive rule-return declaration descriptors from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying descriptor metadata.</param>");
        sb.AppendLine("    /// <returns>Rule-return declaration descriptors, or an empty list when no descriptor metadata is available.</returns>");
        sb.AppendLine("    /// <remarks>The descriptors contain raw declarations only; no typed return properties or implicit variables are generated.</remarks>");
        sb.AppendLine("    private static IReadOnlyList<ParserRuleReturnDescriptor> GetRuleReturnDescriptors(ParserRuleLifecycleContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine();
        sb.AppendLine("        return context.InvocationFrame?.Descriptor?.Returns ?? global::System.Array.Empty<ParserRuleReturnDescriptor>();");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
