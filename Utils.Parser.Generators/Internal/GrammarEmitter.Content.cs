using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Generators.Internal;

internal static partial class GrammarEmitter
{

    // ── Content emission ─────────────────────────────────────────────

    private static void EmitContent(StringBuilder sb, G4Content content, int indent)
    {
        string pad = new string(' ', indent * 4);

        switch (content)
        {
            case G4Alternation alt:
                EmitAlternation(sb, alt, indent);
                break;

            case G4Sequence seq:
                EmitSequence(sb, seq, indent);
                break;

            case G4Quantifier q:
                sb.Append("new Quantifier(");
                EmitContent(sb, q.Inner, indent);
                sb.Append($", {q.Min}, {(q.Max.HasValue ? q.Max.Value.ToString() : "null")}");
                if (!q.Greedy) sb.Append(", false");
                sb.Append(")");
                break;

            case G4LiteralMatch lit:
                sb.Append($"new LiteralMatch(\"{Escape(lit.Value)}\")");
                break;

            case G4RangeMatch range:
                sb.Append($"new RangeMatch({CharLiteral(range.From)}, {CharLiteral(range.To)})");
                break;

            case G4CharClassMatch cc:
                EmitCharClass(sb, cc);
                break;

            case G4AnyCharMatch:
                sb.Append("new AnyChar()");
                break;

            case G4RuleRef rr:
                if (rr.LabelName != null)
                {
                    string labelArg = $"new RuleLabel(\"{Escape(rr.LabelName)}\", \"{Escape(rr.RuleName)}\", {(rr.LabelIsAdditive ? "true" : "false")})";
                    if (rr.RawArguments != null)
                        sb.Append($"new RuleRef(\"{Escape(rr.RuleName)}\", Label: {labelArg}, RawArguments: \"{Escape(rr.RawArguments)}\")");
                    else
                        sb.Append($"new RuleRef(\"{Escape(rr.RuleName)}\", Label: {labelArg})");
                }
                else if (rr.RawArguments != null)
                    sb.Append($"new RuleRef(\"{Escape(rr.RuleName)}\", RawArguments: \"{Escape(rr.RawArguments)}\")");
                else
                    sb.Append($"new RuleRef(\"{Escape(rr.RuleName)}\")");
                break;

            case G4Negation neg:
                sb.Append("new Negation(");
                EmitContent(sb, neg.Inner, indent);
                sb.Append(")");
                break;

            case G4LexerCommand cmd:
                EmitLexerCommand(sb, cmd);
                break;

            case G4EmbeddedAction act:
                if (act.IsPredicate)
                    sb.Append($"new ValidatingPredicate(\"{Escape(act.Code)}\")");
                else
                    sb.Append($"new EmbeddedAction(\"{Escape(act.Code)}\", ActionContext.Alternative, ActionPosition.Inline, new LabelRef[0])");
                break;
        }
    }

    private static void EmitAlternation(StringBuilder sb, G4Alternation alt, int indent)
    {
        if (alt.Alternatives.Count == 1 && alt.Alternatives[0].Items.Count == 1)
        {
            // Single-alternative, single-item: emit content directly wrapped in a minimal Alternation
            EmitSingleAltWrap(sb, alt.Alternatives[0], indent);
            return;
        }

        sb.AppendLine("new Alternation(new Alternative[]");
        sb.AppendLine(new string(' ', indent * 4) + "{");

        for (int i = 0; i < alt.Alternatives.Count; i++)
        {
            var alternative = alt.Alternatives[i];
            sb.Append(new string(' ', (indent + 1) * 4));
            EmitAlternative(sb, alternative, indent + 1);
            if (i < alt.Alternatives.Count - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }

        sb.Append(new string(' ', indent * 4) + "})");
    }

    private static void EmitSingleAltWrap(StringBuilder sb, G4Alternative alt, int indent)
    {
        sb.AppendLine("new Alternation(new Alternative[]");
        sb.AppendLine(new string(' ', indent * 4) + "{");
        sb.Append(new string(' ', (indent + 1) * 4));
        EmitAlternative(sb, alt, indent + 1);
        sb.AppendLine();
        sb.Append(new string(' ', indent * 4) + "})");
    }

    private static void EmitAlternative(StringBuilder sb, G4Alternative alt, int indent)
    {
        string pad = new string(' ', indent * 4);
        sb.Append($"new Alternative({alt.Priority}, Associativity.Left, ");

        var items = alt.Items;

        if (items.Count == 0)
        {
            sb.Append("new Sequence(new RuleContent[0])");
        }
        else if (items.Count == 1 && !(items[0] is G4Sequence))
        {
            EmitContent(sb, items[0], indent + 1);
        }
        else
        {
            EmitSequenceFromItems(sb, items, indent + 1);
        }

        sb.Append(")");
    }

    private static void EmitSequence(StringBuilder sb, G4Sequence seq, int indent)
    {
        EmitSequenceFromItems(sb, seq.Items, indent);
    }

    private static void EmitSequenceFromItems(StringBuilder sb, List<G4Content> items, int indent)
    {
        if (items.Count == 1)
        {
            EmitContent(sb, items[0], indent);
            return;
        }

        string pad  = new string(' ', indent * 4);
        string pad1 = new string(' ', (indent + 1) * 4);

        sb.AppendLine("new Sequence(new RuleContent[]");
        sb.AppendLine(pad + "{");

        for (int i = 0; i < items.Count; i++)
        {
            sb.Append(pad1);
            EmitContent(sb, items[i], indent + 1);
            if (i < items.Count - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }

        sb.Append(pad + "})");
    }

    private static void EmitCharClass(StringBuilder sb, G4CharClassMatch cc)
    {
        sb.Append($"new CharSetMatch(new HashSet<char> {{");
        bool first = true;
        foreach (var (lo, hi) in cc.Entries)
        {
            if (hi.HasValue)
            {
                // Range: expand all chars
                for (char c = lo; c <= hi.Value; c++)
                {
                    if (!first) sb.Append(", ");
                    sb.Append(CharLiteral(c));
                    first = false;
                }
            }
            else
            {
                if (!first) sb.Append(", ");
                sb.Append(CharLiteral(lo));
                first = false;
            }
        }
        sb.Append($"}}, {(cc.Negated ? "true" : "false")})");
    }

    private static void EmitLexerCommand(StringBuilder sb, G4LexerCommand cmd)
    {
        string cmdType = cmd.Name.ToLowerInvariant() switch
        {
            "skip"     => "LexerCommandType.Skip",
            "more"     => "LexerCommandType.More",
            "channel"  => "LexerCommandType.Channel",
            "type"     => "LexerCommandType.Type",
            "pushmode" => "LexerCommandType.PushMode",
            "popmode"  => "LexerCommandType.PopMode",
            "mode"     => "LexerCommandType.Mode",
            _          => $"LexerCommandType.Skip /* unknown: {cmd.Name} */"
        };

        string arg = cmd.Arg != null ? $"\"{Escape(cmd.Arg)}\"" : "null";
        sb.Append($"new LexerCommand({cmdType}, {arg})");
    }
}
