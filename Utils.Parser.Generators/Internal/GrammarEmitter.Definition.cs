using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{

    private static void EmitGrammarOptions(StringBuilder sb, G4Grammar grammar, int indent)
    {
        string pad = new string(' ', indent * 4);

        if (grammar.Options.Count == 0)
        {
            sb.AppendLine($"{pad}Options: null,");
            return;
        }

        sb.AppendLine($"{pad}Options: new GrammarOptions(new Dictionary<string, string>");
        sb.AppendLine($"{pad}{{");

        int optionIndex = 0;
        foreach (var option in grammar.Options.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            string suffix = optionIndex < grammar.Options.Count - 1 ? "," : "";
            sb.AppendLine($"{pad}    [\"{Escape(option.Key)}\"] = \"{Escape(option.Value)}\"{suffix}");
            optionIndex++;
        }

        sb.AppendLine($"{pad}}}),");
    }

    // ── Rule emission ────────────────────────────────────────────────

    private static void EmitRule(StringBuilder sb, G4Rule rule)
    {
        string varName = $"_{Sanitize(rule.Name)}";
        sb.Append($"        var {varName} = new Rule(");
        sb.Append($"\"{Escape(rule.Name)}\", _order++, {(rule.IsFragment ? "true" : "false")}, ");
        EmitContent(sb, rule.Content, 2);
        if (rule.Parameters != null)
        {
            sb.Append($", Parameters: new global::Utils.Parser.Model.RuleParameter[] {{ new global::Utils.Parser.Model.RuleParameter(\"{Escape(rule.Parameters)}\", \"{Escape(rule.Parameters)}\") }}");
        }

        if (rule.Returns != null)
        {
            sb.Append($", Returns: new global::Utils.Parser.Model.RuleReturn[] {{ new global::Utils.Parser.Model.RuleReturn(\"{Escape(rule.Returns)}\", \"{Escape(rule.Returns)}\") }}");
        }

        if (rule.Locals.Count > 0)
        {
            sb.Append(", Locals: new RuleLocal[] { ");
            sb.Append(string.Join(", ", rule.Locals.Select(static local => $"new RuleLocal(\"{Escape(local)}\")")));
            sb.Append(" }");
        }

        sb.AppendLine(");");
    }
}
