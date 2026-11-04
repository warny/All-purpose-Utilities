using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace ExpressionSimplifierAudit;

/// <summary>
/// Rule inventory (brief §3): the rules of <see cref="ExpressionSimplifier"/> in dispatch-plan order (the order
/// <c>ExpressionTransformer.BuildPlan</c> reads them), their signatures and parameter constraints, their source location (from
/// the portable PDB), and what their bodies call (found by scanning their IL, transitively through the simplifier's own private
/// helpers): engine re-entries, comparer calls, effect analysis, operator-safety guards, folding and node factories. From the
/// re-entries and factories a static "may reach" graph is built and its strongly connected components are listed; the dynamic
/// graph observed by the termination campaign is analysed the same way.
/// </summary>
internal static class Inventory
{
    /// <summary>The binding flags <c>BuildPlan</c> uses.</summary>
    private const BindingFlags PlanFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod | BindingFlags.Instance;

    /// <summary>The IL opcodes by value.</summary>
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    /// <summary>One rule of the plan.</summary>
    /// <param name="Index">The plan index.</param>
    /// <param name="Method">The method.</param>
    /// <param name="NodeType">The declared node type.</param>
    /// <param name="Signature">The method-level signature attribute.</param>
    public sealed record Rule(int Index, MethodInfo Method, ExpressionType NodeType, ExpressionSignatureAttribute Signature)
    {
        /// <summary>Gets the rule identifier as the trace prints it (name, completed with the parameter types when overloaded).</summary>
        public string Id { get; init; } = "";
    }

    /// <summary>Lists the rules in plan order.</summary>
    /// <returns>The rules.</returns>
    public static List<Rule> Rules()
    {
        List<Rule> rules = typeof(ExpressionSimplifier).GetMethods(PlanFlags)
            .Select(m => (m, attr: m.GetCustomAttributes<ExpressionSignatureAttribute>().FirstOrDefault()))
            .Where(t => t.attr is not null)
            .Select((t, i) => new Rule(i, t.m, t.attr!.ExpressionType, t.attr!))
            .ToList();
        var counts = rules.GroupBy(r => r.Method.Name).ToDictionary(g => g.Key, g => g.Count());
        return rules.Select(r => r with
        {
            Id = counts[r.Method.Name] > 1
                ? r.Method.Name + "(" + string.Join(", ", r.Method.GetParameters().Select(p => p.ParameterType.Name)) + ")"
                : r.Method.Name,
        }).ToList();
    }

    /// <summary>Writes the inventory, the static graph and its components.</summary>
    /// <param name="writer">The results.</param>
    /// <param name="dynamicEdges">The dynamic edges file of a termination campaign, or <see langword="null"/>.</param>
    /// <returns>0.</returns>
    public static int Run(ResultWriter writer, string? dynamicEdges)
    {
        List<Rule> rules = Rules();
        using var pdb = PdbLocator.Open(typeof(ExpressionSimplifier).Assembly);
        var md = new StringBuilder("# Rule inventory\n\n");
        md.Append("Rules in plan order (").Append(rules.Count).Append("). Columns: re-entry (`TransformCore`, `TransformCoreWithPreparedOperands`), comparer calls, effect analysis, ")
          .Append("operator guards, constant folding, node factories (transitively through the simplifier's private helpers).\n\n");
        md.Append("| # | Rule | Node | Parameter constraints | Location | Re-entry | Comparer | Effects | Guards | Folding | Builds |\n|---:|---|---|---|---|---|---|---|---|---|---|\n");
        var features = new Dictionary<Rule, Features>();
        foreach (Rule rule in rules)
        {
            Features f = Scan(rule.Method);
            features[rule] = f;
            string constraints = string.Join("; ", rule.Method.GetParameters().Skip(1).Select(p => p.ParameterType.Name + Constraint(p)));
            string location = pdb?.Locate(rule.Method) ?? "?";
            writer.Record(new
            {
                rule.Index, rule.Id, node = rule.NodeType.ToString(), constraints, location,
                reentry = f.Reentry.ToArray(), comparer = f.Comparer.ToArray(), effects = f.Effects.ToArray(), guards = f.Guards.ToArray(), folding = f.Folding.ToArray(), builds = f.Builds.ToArray(),
            });
            md.Append("| ").Append(rule.Index).Append(" | `").Append(rule.Id).Append("` | ").Append(rule.NodeType).Append(" | ").Append(constraints).Append(" | ").Append(location)
              .Append(" | ").Append(Join(f.Reentry)).Append(" | ").Append(Join(f.Comparer)).Append(" | ").Append(Join(f.Effects)).Append(" | ").Append(Join(f.Guards))
              .Append(" | ").Append(Join(f.Folding)).Append(" | ").Append(Join(f.Builds)).Append(" |\n");
        }

        // Static graph: a rule that re-enters the engine on a node it builds may reach every rule of that node type; a rule that
        // compares through the public comparer may reach every rule (the comparer simplifies both operands).
        var graph = new Dictionary<string, HashSet<string>>();
        foreach (Rule rule in rules)
        {
            var targets = new HashSet<string>();
            Features f = features[rule];
            if (f.Reentry.Count > 0)
            {
                foreach (string built in f.Builds)
                {
                    if (!Enum.TryParse(built, out ExpressionType type)) continue;
                    foreach (Rule target in rules.Where(r => r.NodeType == type)) targets.Add(target.Id);
                }
            }

            if (f.Comparer.Count > 0) targets.Add("(comparer simplification)");
            graph[rule.Id] = targets;
        }

        graph["(comparer simplification)"] = rules.Select(r => r.Id).ToHashSet();
        md.Append("\n## Static may-reach graph\n\nEdges A → B when A re-enters the engine on a node type B is declared on (B may then run on the rebuilt node). ")
          .Append("Every rule that compares operands reaches the pseudo-node `(comparer simplification)`, which reaches every rule.\n\n");
        AppendComponents(md, graph, "Static graph without the comparer node", exclude: "(comparer simplification)");
        AppendComponents(md, graph, "Static graph with the comparer node", exclude: null);
        md.Append("\n| Rule | Static successors (without the comparer) |\n|---|---|\n");
        foreach (KeyValuePair<string, HashSet<string>> pair in graph.Where(p => p.Key != "(comparer simplification)" && p.Value.Count > 0))
        {
            md.Append("| `").Append(pair.Key).Append("` | ").Append(string.Join(", ", pair.Value.Where(t => t != "(comparer simplification)").OrderBy(t => t).Select(t => "`" + t + "`"))).Append(" |\n");
        }

        if (dynamicEdges is not null && File.Exists(dynamicEdges))
        {
            var dynamic = new Dictionary<string, HashSet<string>>();
            var consumed = new Dictionary<string, HashSet<string>>();
            md.Append("\n## Dynamic graph (observed by the termination campaign)\n\n`via`: B applied inside a treatment A requested. `consumed`: B applied to the parent of a node A rewrote (a correlation, not a proof).\n\n| Kind | A | B | Occurrences |\n|---|---|---|---:|\n");
            foreach (string line in File.ReadLines(dynamicEdges))
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 2) continue;
                string[] edge = parts[0].Split('|');
                Dictionary<string, HashSet<string>> target = edge[0] == "via" ? dynamic : consumed;
                if (!target.TryGetValue(edge[1], out HashSet<string>? set)) target[edge[1]] = set = new HashSet<string>();
                set.Add(edge[2]);
                md.Append("| ").Append(edge[0]).Append(" | `").Append(edge[1]).Append("` | `").Append(edge[2]).Append("` | ").Append(parts[1]).Append(" |\n");
            }

            AppendComponents(md, dynamic, "Dynamic re-entry graph (via)", exclude: null);
            var union = dynamic.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value));
            foreach (KeyValuePair<string, HashSet<string>> pair in consumed)
            {
                if (!union.TryGetValue(pair.Key, out HashSet<string>? set)) union[pair.Key] = set = new HashSet<string>();
                set.UnionWith(pair.Value);
            }

            AppendComponents(md, union, "Dynamic graph (via and consumed)", exclude: null);
        }

        writer.Summary(md.ToString());
        return 0;
    }

    /// <summary>Appends the non-trivial strongly connected components of a graph (Tarjan).</summary>
    /// <param name="md">The output.</param>
    /// <param name="graph">The graph.</param>
    /// <param name="title">The section title.</param>
    /// <param name="exclude">A node to remove first, or <see langword="null"/>.</param>
    private static void AppendComponents(StringBuilder md, Dictionary<string, HashSet<string>> graph, string title, string? exclude)
    {
        var nodes = graph.Keys.Concat(graph.Values.SelectMany(v => v)).Where(n => n != exclude).Distinct().ToList();
        var index = new Dictionary<string, int>();
        var low = new Dictionary<string, int>();
        var stack = new Stack<string>();
        var onStack = new HashSet<string>();
        var components = new List<List<string>>();
        int counter = 0;
        void Strong(string v)
        {
            index[v] = low[v] = counter++;
            stack.Push(v);
            onStack.Add(v);
            foreach (string w in graph.TryGetValue(v, out HashSet<string>? next) ? next.Where(n => n != exclude) : [])
            {
                if (!index.ContainsKey(w))
                {
                    Strong(w);
                    low[v] = Math.Min(low[v], low[w]);
                }
                else if (onStack.Contains(w))
                {
                    low[v] = Math.Min(low[v], index[w]);
                }
            }

            if (low[v] != index[v]) return;
            var component = new List<string>();
            string x;
            do
            {
                x = stack.Pop();
                onStack.Remove(x);
                component.Add(x);
            }
            while (x != v);
            bool selfLoop = component.Count == 1 && graph.TryGetValue(v, out HashSet<string>? own) && own.Contains(v);
            if (component.Count > 1 || selfLoop) components.Add(component);
        }

        foreach (string node in nodes) if (!index.ContainsKey(node)) Strong(node);
        md.Append("\n### ").Append(title).Append("\n\n");
        if (components.Count == 0)
        {
            md.Append("No cycle.\n");
            return;
        }

        md.Append("| Component | Size | Members |\n|---:|---:|---|\n");
        int c = 0;
        foreach (List<string> component in components.OrderByDescending(x => x.Count))
        {
            md.Append("| ").Append(++c).Append(" | ").Append(component.Count).Append(" | ").Append(string.Join(", ", component.OrderBy(n => n).Select(n => "`" + n + "`"))).Append(" |\n");
        }
    }

    /// <summary>Joins a feature set.</summary>
    /// <param name="set">The set.</param>
    /// <returns>The text, or a dash.</returns>
    private static string Join(SortedSet<string> set) => set.Count == 0 ? "—" : string.Join(", ", set);

    /// <summary>Describes the constraint attributes of a rule parameter.</summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>The text.</returns>
    private static string Constraint(ParameterInfo parameter)
    {
        var parts = new List<string>();
        foreach (CustomAttributeData data in parameter.GetCustomAttributesData())
        {
            string args = string.Join(",", data.ConstructorArguments.Select(a => a.Value is System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument> list ? "[" + string.Join(",", list.Select(i => i.Value)) + "]" : a.Value?.ToString()));
            parts.Add(data.AttributeType.Name.Replace("Attribute", "") + "(" + args + ")");
        }

        return parts.Count == 0 ? "" : " [" + string.Join(" ", parts) + "]";
    }

    /// <summary>The calls found in a rule body.</summary>
    private sealed class Features
    {
        /// <summary>Gets the engine re-entries.</summary>
        public SortedSet<string> Reentry { get; } = new();

        /// <summary>Gets the comparer calls.</summary>
        public SortedSet<string> Comparer { get; } = new();

        /// <summary>Gets the effect-analysis calls.</summary>
        public SortedSet<string> Effects { get; } = new();

        /// <summary>Gets the operator-safety guards.</summary>
        public SortedSet<string> Guards { get; } = new();

        /// <summary>Gets the constant-folding helpers.</summary>
        public SortedSet<string> Folding { get; } = new();

        /// <summary>Gets the node types built with the <see cref="Expression"/> factories.</summary>
        public SortedSet<string> Builds { get; } = new();
    }

    /// <summary>Scans a rule body and, transitively (depth 4), the simplifier's own helpers it calls.</summary>
    /// <param name="method">The rule.</param>
    /// <returns>The features.</returns>
    private static Features Scan(MethodInfo method)
    {
        var features = new Features();
        var visited = new HashSet<MethodBase>();
        void Walk(MethodBase m, int depth)
        {
            if (!visited.Add(m) || depth > 4) return;
            foreach (MethodBase callee in Callees(m))
            {
                string name = callee.Name;
                Type? owner = callee.DeclaringType;
                if (name is "TransformCore" or "TransformCoreWithPreparedOperands" && owner?.Name == "ExpressionTransformer") features.Reentry.Add(name);
                else if (owner == typeof(ExpressionComparer)) features.Comparer.Add(name);
                else if (owner?.Name is "FactorEqualityProbe") features.Comparer.Add("FactorEqualityProbe." + name);
                else if (owner?.Name is "ExpressionEffectAnalysis") features.Effects.Add(name);
                else if (owner == typeof(Expression) && callee is MethodInfo factory && Enum.TryParse(name, out ExpressionType built)) features.Builds.Add(built.ToString());
                else if (owner == typeof(Expression) && name == "Call") features.Builds.Add("Call");

                if (name.StartsWith("IsOrdinary", StringComparison.Ordinal) || name is "IsConstantPair" or "IsSignedIntegral" or "IsZeroDivisorOutsideFieldDomain" or "CanCombineUnaryLogCalls") features.Guards.Add(name);
                if (name is "FoldConstants" or "TryFoldFactoredCoefficient" or "AggregateFactoredCoefficient" or "NumericValueEquals") features.Folding.Add(name);
                if (name == "EqualsKeepingSimplifiedForms") features.Comparer.Add(name);
                if (name == "BuildFactoredProduct") features.Folding.Add(name);

                bool own = owner is not null && (owner == typeof(ExpressionSimplifier) || owner.DeclaringType == typeof(ExpressionSimplifier));
                if (own && callee != method) Walk(callee, depth + 1);
            }
        }

        Walk(method, 0);
        return features;
    }

    /// <summary>Lists the methods a method body calls (call, callvirt, newobj).</summary>
    /// <param name="method">The method.</param>
    /// <returns>The callees.</returns>
    private static IEnumerable<MethodBase> Callees(MethodBase method)
    {
        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null) yield break;
        Type[]? typeArguments = method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null;
        Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        int position = 0;
        while (position < il.Length)
        {
            short value = il[position++];
            if (value == 0xFE) value = (short)(0xFE00 | il[position++]);
            if (!OpCodesByValue.TryGetValue(value, out OpCode opcode)) yield break;
            int operandSize = opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, position),
                _ => 4,
            };
            if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt || opcode == OpCodes.Newobj || opcode == OpCodes.Ldftn)
            {
                int token = BitConverter.ToInt32(il, position);
                MethodBase? callee = null;
                try
                {
                    callee = method.Module.ResolveMethod(token, typeArguments, methodArguments);
                }
                catch (ArgumentException)
                {
                }

                if (callee is not null) yield return callee;
            }

            position += operandSize;
        }
    }
}

/// <summary>Reads source locations from the portable PDB next to an assembly.</summary>
internal sealed class PdbLocator : IDisposable
{
    /// <summary>The metadata reader provider.</summary>
    private readonly MetadataReaderProvider _provider;

    /// <summary>The PDB reader.</summary>
    private readonly MetadataReader _reader;

    /// <summary>Creates a locator.</summary>
    /// <param name="provider">The provider.</param>
    private PdbLocator(MetadataReaderProvider provider)
    {
        _provider = provider;
        _reader = provider.GetMetadataReader();
    }

    /// <summary>Opens the portable PDB of an assembly, or returns <see langword="null"/> when there is none.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The locator, or <see langword="null"/>.</returns>
    public static PdbLocator? Open(Assembly assembly)
    {
        string path = Path.ChangeExtension(assembly.Location, ".pdb");
        if (!File.Exists(path)) return null;
        try
        {
            return new PdbLocator(MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(path)));
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>Returns <c>file:line</c> of the first sequence point of a method.</summary>
    /// <param name="method">The method.</param>
    /// <returns>The location, or <see langword="null"/>.</returns>
    public string? Locate(MethodInfo method)
    {
        MethodDefinitionHandle handle = MetadataTokens.MethodDefinitionHandle(method.MetadataToken);
        MethodDebugInformation info = _reader.GetMethodDebugInformation(handle);
        foreach (SequencePoint point in info.GetSequencePoints())
        {
            if (point.IsHidden) continue;
            string document = _reader.GetString(_reader.GetDocument(point.Document).Name);
            return Path.GetFileName(document) + ":" + point.StartLine;
        }

        return null;
    }

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();
}
