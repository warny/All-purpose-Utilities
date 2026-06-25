using System.Text;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{


    /// <summary>
    /// Emits generated facade helpers that opt in to generated embedded-code execution through a per-parse context.
    /// </summary>
    /// <param name="sb">Source builder receiving generated C#.</param>
    /// <param name="className">Generated grammar class name.</param>
    private static void EmitEmbeddedCodeFacade(StringBuilder sb, string className)
    {
        string contextClassName = GetExecutionContextClassName(className);

        sb.AppendLine("    /// <summary>Creates a runtime policy bound to the supplied execution context. Reusing the policy reuses that context state.</summary>");
        sb.AppendLine("    /// <param name=\"executionContext\">Execution context instance that owns generated hooks and injected parser members.</param>");
        sb.AppendLine("    /// <param name=\"basePolicy\">Optional policy whose non-embedded-code components are preserved.</param>");
        sb.AppendLine("    /// <returns>A runtime policy bound to <paramref name=\"executionContext\"/>.</returns>");
        sb.AppendLine($"    public static ParserRuntimeFeaturePolicy CreateRuntimePolicy({contextClassName} executionContext, ParserRuntimeFeaturePolicy? basePolicy = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(executionContext);");
        sb.AppendLine("        return executionContext.CreateRuntimePolicy(basePolicy);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Parses input using generated C# hooks and injected parser members. Creates a fresh execution context for this parse.</summary>");
        sb.AppendLine($"    public static ParseNode ParseWithEmbeddedCode([global::System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxName, typeof({className}))] string input)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return ParseWithEmbeddedCode(input, new {contextClassName}());");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Parses input using generated C# hooks bound to the supplied execution context. Reusing the same context intentionally preserves its member state across parses.</summary>");
        sb.AppendLine("    /// <param name=\"input\">Input text to parse.</param>");
        sb.AppendLine("    /// <param name=\"executionContext\">Execution context instance that owns generated hooks and injected parser members.</param>");
        sb.AppendLine("    /// <returns>The parse tree produced by the generated grammar.</returns>");
        sb.AppendLine($"    public static ParseNode ParseWithEmbeddedCode([global::System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxName, typeof({className}))] string input, {contextClassName} executionContext)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(executionContext);");
        sb.AppendLine("        var grammar = new CompiledGrammar(Build(), executionContext.CreateRuntimePolicy());");
        sb.AppendLine("        return grammar.Parse(input);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Parses input using generated C# hooks plus an explicit base runtime policy. Existing embedded-code components are replaced while other base-policy components, including rule-call execution, are preserved.</summary>");
        sb.AppendLine("    /// <param name=\"input\">Input text to parse.</param>");
        sb.AppendLine("    /// <param name=\"executionContext\">Execution context instance that owns generated hooks and injected parser members.</param>");
        sb.AppendLine("    /// <param name=\"basePolicy\">Base runtime policy whose non-embedded-code components are preserved.</param>");
        sb.AppendLine("    /// <returns>The parse tree produced by the generated grammar.</returns>");
        sb.AppendLine($"    public static ParseNode ParseWithEmbeddedCode([global::System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxName, typeof({className}))] string input, {contextClassName} executionContext, ParserRuntimeFeaturePolicy basePolicy)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(executionContext);");
        sb.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(basePolicy);");
        sb.AppendLine("        var grammar = new CompiledGrammar(Build(), executionContext.CreateRuntimePolicy(basePolicy));");
        sb.AppendLine("        return grammar.Parse(input);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
