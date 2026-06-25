using System.Text;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{

    /// <summary>
    /// Emits explicit rule-parameter helper methods on the generated execution context.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    private static void EmitRuleParameterHelpers(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>Gets an untyped rule-parameter value from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying the invocation frame to inspect.</param>");
        sb.AppendLine("    /// <param name=\"name\">Rule-parameter metadata name.</param>");
        sb.AppendLine("    /// <returns>The stored parameter value, or <c>null</c> when no invocation frame or parameter value is present.</returns>");
        sb.AppendLine("    /// <remarks>Parameters are not auto-bound. Rule call arguments are not evaluated by this untyped helper; read-only <c>$param</c> uses a generated typed required helper when descriptor metadata exposes a raw type.</remarks>");
        sb.AppendLine("    private static object? GetRuleParameter(ParserRuleLifecycleContext context, string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(name);");
        sb.AppendLine();
        sb.AppendLine("        return TryGetRuleParameter(context, name, out object? value) ? value : null;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Attempts to get an untyped rule-parameter value from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying the invocation frame to inspect.</param>");
        sb.AppendLine("    /// <param name=\"name\">Rule-parameter metadata name.</param>");
        sb.AppendLine("    /// <param name=\"value\">Receives the stored parameter value when explicitly present on the frame.</param>");
        sb.AppendLine("    /// <returns><c>true</c> when the invocation frame contains the named parameter; otherwise, <c>false</c>.</returns>");
        sb.AppendLine("    /// <remarks>Returns <c>false</c> when no argument was explicitly supplied. Parameters are not auto-bound.</remarks>");
        sb.AppendLine("    private static bool TryGetRuleParameter(ParserRuleLifecycleContext context, string name, out object? value)");
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
        sb.AppendLine("        return context.InvocationFrame.TryGetParameter(name, out value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets a required typed rule-parameter value from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    private static T GetRequiredRuleParameter<T>(ParserRuleLifecycleContext context, string parameterName)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(parameterName);");
        sb.AppendLine("        if (context.InvocationFrame is null || !context.InvocationFrame.TryGetParameter(parameterName, out object? value))");
        sb.AppendLine("        {");
        sb.AppendLine(@"            throw new global::Utils.Parser.Runtime.ParserAttributeAccessException(""$"" + parameterName, parameterName, ""Rule parameter '"" + parameterName + ""' is not available on the current rule invocation."");");
        sb.AppendLine("        }");
        sb.AppendLine("        return CastRequiredRuleAttribute<T>(\"$\" + parameterName, \"parameter\", parameterName, value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Casts a required typed rule attribute value and reports deterministic attribute access failures.</summary>");
        sb.AppendLine("    private static T CastRequiredRuleAttribute<T>(string attributeText, string attributeKind, string name, object? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is T typed) return typed;");
        sb.AppendLine("        if (value is null && default(T) is null) return default!;");
        sb.AppendLine(@"        string actualType = value is null ? ""null"" : value.GetType().FullName ?? value.GetType().Name;");
        sb.AppendLine(@"        throw new global::Utils.Parser.Runtime.ParserAttributeAccessException(attributeText, name, ""Rule "" + attributeKind + "" '"" + name + ""' cannot be read as '"" + typeof(T).FullName + ""' because the stored value is '"" + actualType + ""'."");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets passive rule-parameter declaration descriptors from the active lifecycle invocation frame.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Lifecycle context carrying descriptor metadata.</param>");
        sb.AppendLine("    /// <returns>Rule-parameter declaration descriptors, or an empty list when no descriptor metadata is available.</returns>");
        sb.AppendLine("    /// <remarks>Descriptors contain raw declarations only. No typed parameter fields/properties are generated.</remarks>");
        sb.AppendLine("    private static IReadOnlyList<ParserRuleParameterDescriptor> GetRuleParameterDescriptors(ParserRuleLifecycleContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine();
        sb.AppendLine("        return context.InvocationFrame?.Descriptor?.Parameters ?? global::System.Array.Empty<ParserRuleParameterDescriptor>();");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
